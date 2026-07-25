using System.Threading.RateLimiting;
using Desk.Api.Auth;
using Desk.Api.Middleware;
using Desk.Application.Abstractions;
using Desk.Infrastructure;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging (JSON to console; OTLP exporter is added below for traces/metrics).
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

var config = builder.Configuration;

// ---- Infrastructure (DbContext, tenant context, secret store) ----
builder.Services.AddDeskInfrastructure(config);

// ---- Connectors ----
// Real Autotask and ConnectWise factories are registered by AddDeskInfrastructure. Both Wave-1
// providers now have production connectors; the mock is retained only for tests.

// ---- Identity plumbing ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IClaimsTransformation, DeskClaimsTransformation>();

// ---- AuthN: validate Keycloak-issued JWTs ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = config["Keycloak:Authority"];
        options.Audience = config["Keycloak:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = !string.IsNullOrEmpty(config["Keycloak:Audience"]);
        options.TokenValidationParameters.ValidateLifetime = true;
    });

// ---- AuthZ: permission-claim policies ----
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ---- API surface ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Health ----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DeskDbContext>("database");

// ---- Rate limiting (global fixed window) ----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.User.FindFirst(CurrentUser.OrgClaim)?.Value
                          ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));
});

// ---- CORS (allowlist from config) ----
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ---- Request size cap ----
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 25 * 1024 * 1024);

// NOTE: OpenTelemetry OTLP export is deferred to the observability phase. The current
// OTLP exporter package carries an unpatched moderate advisory (GHSA-4625-4j76-fww9) and
// the strict NU1902 gate (TreatWarningsAsErrors) rightly blocks it. Structured Serilog logs
// with correlation ids cover Phase-2 observability.

var app = builder.Build();

// Production must not fall back to the in-memory secret store.
if (app.Environment.IsProduction() &&
    app.Services.GetRequiredService<ISecretStore>() is InMemorySecretStore)
{
    throw new InvalidOperationException("Refusing to start in Production without a configured Vault secret store.");
}

// ---- Pipeline order matters ----
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseCors("web");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>(); // after auth, before controllers
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();

/// <summary>Exposed so the integration/unit test host can reference the API entrypoint.</summary>
public partial class Program;
