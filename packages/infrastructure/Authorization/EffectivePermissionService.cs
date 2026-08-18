using Desk.Application.Authorization;
using Desk.Domain.Authorization;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Authorization;

/// <summary>
/// Resolves effective permissions per the algorithm fixed in the RBAC design:
///
///   1. Union the scope of this permission across every role the user holds, keeping the most
///      permissive one. Multiple roles have always unioned their CLAIMS (DeskClaimsTransformation
///      already does `SelectMany(...).Distinct()`); this is the scope-aware version of the same rule.
///   2. A user-level override, if one exists, REPLACES that result outright — Deny becomes None and
///      stops; Grant substitutes its own scope. It never merges with the role scope, because a
///      merged answer ("Department from the role, but also Assigned from the override — take the
///      wider?") has no single right combination rule and is unexplainable to an admin reading it
///      back later. One override row is meant to be the complete, self-contained reason.
///   3. Board access is resolved independently and returned alongside, never combined into Scope —
///      callers AND the two fences themselves.
/// </summary>
public sealed class EffectivePermissionService(DeskDbContext db) : IEffectivePermissionService
{
    // All=100 down to None=0 — highest number wins when unioning across roles. Deliberately NOT the
    // same as the PermissionScope enum's own numeric values (those are declaration order, not rank).
    private static readonly Dictionary<PermissionScope, int> Rank = new()
    {
        [PermissionScope.All] = 100,
        [PermissionScope.Department] = 80,
        [PermissionScope.Team] = 60,
        [PermissionScope.Selected] = 50,
        [PermissionScope.Assigned] = 40,
        [PermissionScope.Own] = 40,
        [PermissionScope.None] = 0,
    };

    public async Task<EffectivePermission> ResolveAsync(Guid appUserId, string permissionKey, CancellationToken ct = default)
    {
        var roleScopes = await db.UserRoles
            .Where(ur => ur.AppUserId == appUserId)
            .Join(db.Set<Desk.Domain.Identity.RolePermission>(), ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp)
            .Where(rp => rp.PermissionKey == permissionKey)
            .Select(rp => rp.Scope)
            .ToListAsync(ct);

        var (scope, source) = roleScopes.Count == 0
            ? (PermissionScope.None, PermissionSource.NoGrant)
            : (roleScopes.OrderByDescending(s => Rank[s]).First(), PermissionSource.RoleGrant);

        var overrideRow = await db.UserPermissionOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.AppUserId == appUserId && o.PermissionKey == permissionKey, ct);

        if (overrideRow is not null)
        {
            (scope, source) = overrideRow.Effect == PermissionEffect.Deny
                ? (PermissionScope.None, PermissionSource.OverrideDeny)
                : (overrideRow.Scope ?? PermissionScope.All, PermissionSource.OverrideGrant);
        }

        var (mode, grants) = PermissionCatalog.TryGet(permissionKey, out var def) && def!.IsBoardAware
            ? await ResolveBoardAccessAsync(appUserId, def, ct)
            : (BoardAccessMode.All, (IReadOnlyList<BoardGrant>)[]);

        return new EffectivePermission(permissionKey, scope, source, mode, grants);
    }

    private async Task<(BoardAccessMode Mode, IReadOnlyList<BoardGrant> Grants)> ResolveBoardAccessAsync(
        Guid appUserId, PermissionDefinition def, CancellationToken ct)
    {
        var access = await db.UserBoardAccesses.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AppUserId == appUserId, ct);
        // Absent row means All, not None — the fail-open default fixed in Phase 1, so shipping this
        // engine cannot itself take ticket access away from anyone before an admin UI exists to
        // grant it back deliberately.
        var mode = access?.Mode ?? BoardAccessMode.All;
        if (mode != BoardAccessMode.Selected)
            return (mode, []);

        var grants = await db.UserBoardGrants.AsNoTracking()
            .Where(g => g.AppUserId == appUserId && (g.Actions & def.RequiredBoardAction) == def.RequiredBoardAction)
            .Select(g => new BoardGrant(g.PsaConnectionId, g.BoardName, g.Actions))
            .ToListAsync(ct);
        return (mode, grants);
    }
}
