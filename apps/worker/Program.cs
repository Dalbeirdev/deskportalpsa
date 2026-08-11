using Desk.Application.Jobs;
using Desk.Infrastructure;
using Desk.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

builder.Services.AddDeskInfrastructure(builder.Configuration);

// A background process has no HTTP request to derive a caller from; audit rows it produces are
// attributed to the system. Registered here, not in shared DI, so the API keeps its real resolver.
builder.Services.AddScoped<Desk.Application.Abstractions.ICurrentUser, SystemCurrentUser>();

// Job handlers
builder.Services.AddScoped<IJobHandler, InboundEventJobHandler>();

// Connectors — the real Autotask + ConnectWise factories come from AddDeskInfrastructure.

// Hosted services
builder.Services.AddHostedService<BackgroundJobPollingService>();
builder.Services.AddHostedService<PollingSyncService>();
builder.Services.AddHostedService<ScheduledReportService>();

var host = builder.Build();
host.Run();
