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

        // First login for an invited user: no row carries this subject yet, but an active account
        // created by an admin is waiting on the token's VERIFIED email. Bind the subject now, once —
        // afterwards resolution is by subject alone, so a later email change cannot re-bind.
        if (user is null)
        {
            var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
            if (!string.IsNullOrEmpty(email))
            {
                var invited = await db.AppUsers
                    .Include(u => u.Roles)
                    .SingleOrDefaultAsync(u => u.IdpSubject == null && u.IsActive && u.Email.ToLower() == email.ToLower());
                if (invited is not null)
                {
                    invited.IdpSubject = subject;
                    await db.SaveChangesAsync();
                    user = invited;
                }
            }
        }
        if (user is null)
            return principal;

        var roleIds = user.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .Include(r => r.Permissions)
            .ToListAsync();

        var identity = new ClaimsIdentity();

        // Who this is, in both id spaces. Emitted here because this is the one place per request
        // that already has the AppUser loaded — anything scoping data to "this person's own work"
        // would otherwise re-query for it on every call site.
        identity.AddClaim(new Claim(CurrentUser.UserIdClaim, user.Id.ToString()));
        if (!string.IsNullOrEmpty(user.ExternalTechnicianId))
            identity.AddClaim(new Claim(CurrentUser.TechnicianClaim, user.ExternalTechnicianId));

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
