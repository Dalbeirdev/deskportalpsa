using Desk.Api.Auth;
using Desk.Application.Abstractions;

namespace Desk.Api.Middleware;

/// <summary>
/// Establishes the tenant scope for the request from the authenticated principal's claims
/// (added by <see cref="DeskClaimsTransformation"/>). Runs after authentication so the scope
/// is set before any controller/DbContext work. Unauthenticated requests get no scope, which
/// causes the DbContext filter to return zero rows (fail closed).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISettableTenantContext tenant)
    {
        var user = context.User;
        if (user.Identity is { IsAuthenticated: true })
        {
            if (user.HasClaim(c => c.Type == CurrentUser.PlatformScopeClaim))
            {
                tenant.SetPlatformScope();
            }
            else
            {
                var orgClaim = user.FindFirst(CurrentUser.OrgClaim)?.Value;
                if (Guid.TryParse(orgClaim, out var orgId))
                    tenant.SetTenant(orgId);
            }
        }

        await next(context);
    }
}
