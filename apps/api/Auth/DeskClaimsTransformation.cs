using System.Security.Claims;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Auth;

/// <summary>
/// After the JWT is validated, enrich the principal with the DB-authoritative org id and
/// permission claims for the matching internal user. Keeping the DB as the source of truth
/// means access can change without re-issuing tokens. Runs idempotently per request.
/// </summary>
public sealed class DeskClaimsTransformation(DeskDbContext db, TimeProvider clock) : Microsoft.AspNetCore.Authentication.IClaimsTransformation
{
    /// <summary>How stale AppUser.LastActiveAt must be before it's worth a write. This method runs
    /// on every authenticated request (stateless bearer tokens, no session), so writing on every
    /// call would be a write-per-API-call hot path; this keeps it to roughly one write per active
    /// user per window while still giving a meaningfully fresh signal.</summary>
    private static readonly TimeSpan ActivityThrottle = TimeSpan.FromMinutes(5);


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

        var now = clock.GetUtcNow();
        if (user.LastActiveAt is null || now - user.LastActiveAt > ActivityThrottle)
        {
            // A targeted update rather than re-loading the tracked entity: this runs on every
            // authenticated request, so the write itself has to stay as cheap as the throttle it's
            // guarded by is meant to make it.
            await db.AppUsers.Where(u => u.Id == user.Id).ExecuteUpdateAsync(
                s => s.SetProperty(u => u.LastActiveAt, now));
        }

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

        var granted = roles.SelectMany(r => r.Permissions).Select(p => p.PermissionKey).ToHashSet();

        // A per-user override REPLACES the role-derived answer for its key, not just at the
        // fine-grained scope level resolved later by IEffectivePermissionService — the coarse
        // claim-presence gate (`[RequirePermission]`) has to agree with it too, or a Deny override
        // would stop nothing: the endpoint-level check would still see the role's claim and let the
        // request through, leaving only the later, easier-to-forget scope check standing between the
        // caller and the denied action. A Grant override works the same way in reverse — it can hand
        // out a permission no role held at all.
        var overrides = await db.UserPermissionOverrides.AsNoTracking()
            .Where(o => o.AppUserId == user.Id)
            .ToListAsync();
        foreach (var o in overrides)
        {
            if (o.Effect == PermissionEffect.Deny) granted.Remove(o.PermissionKey);
            else granted.Add(o.PermissionKey);
        }

        foreach (var perm in granted)
            identity.AddClaim(new Claim(CurrentUser.PermissionClaim, perm));

        principal.AddIdentity(identity);
        return principal;
    }
}
