using System.Security.Claims;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Auth;

/// <summary>
/// After the JWT is validated, enrich the principal with the DB-authoritative org id and
/// permission claims for the matching internal user. Keeping the DB as the source of truth
/// means access can change without re-issuing tokens. Runs idempotently per request.
/// </summary>
public sealed class DeskClaimsTransformation(DeskDbContext db) : Microsoft.AspNetCore.Authentication.IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true })
            return principal;

        // Already enriched?
        if (principal.HasClaim(c => c.Type == CurrentUser.OrgClaim || c.Type == CurrentUser.PlatformScopeClaim))
            return principal;

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (string.IsNullOrEmpty(subject))
            return principal;

        // AppUser is not tenant-scoped, so this lookup is safe before any tenant scope exists.
        var user = await db.AppUsers
            .AsNoTracking()
            .Include(u => u.Roles)
            .SingleOrDefaultAsync(u => u.IdpSubject == subject && u.IsActive);
        if (user is null)
            return principal;

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .Include(r => r.Permissions)
            .ToListAsync();

        var identity = new ClaimsIdentity();
        var isPlatform = roles.Any(r => r.BuiltInType == RoleType.PlatformSuperAdministrator);
        if (isPlatform)
            identity.AddClaim(new Claim(CurrentUser.PlatformScopeClaim, "true"));
        else if (user.MspOrganizationId is { } org)
            identity.AddClaim(new Claim(CurrentUser.OrgClaim, org.ToString()));

        foreach (var perm in roles.SelectMany(r => r.Permissions).Select(p => p.PermissionKey).Distinct())
            identity.AddClaim(new Claim(CurrentUser.PermissionClaim, perm));

        principal.AddIdentity(identity);
        return principal;
    }
}
