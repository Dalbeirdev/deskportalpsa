using System.Security.Claims;
using System.Text.Encodings.Web;
using Desk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Desk.Api.Auth;

/// <summary>
/// Local-mode ONLY authentication: authenticates every request as the seeded dev admin so the
/// platform can be driven without Keycloak. Wired in exclusively when LocalMode:Enabled is true in
/// the Development environment — it is never registered in production. The real permission set still
/// comes from the database via <see cref="DeskClaimsTransformation"/> for this subject.
/// </summary>
public sealed class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, DatabaseSeeder.DevAdminSubject),
                new Claim("name", "Demo Admin"),
                new Claim(ClaimTypes.Email, "dev-admin@local"),
            ],
            SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
