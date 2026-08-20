using Desk.Application.Abstractions;
using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Admin;

/// <summary>
/// Custom roles for one tenant, layered over the read-only built-in staff roles. The visibility
/// rule mirrors UserAdminService's assignability rule exactly: the 4 staff built-ins plus this
/// tenant's own custom roles — client and platform roles are neither shown nor manageable here.
/// </summary>
public sealed class RoleAdminService(
    DeskDbContext db, IAuditWriter audit, ITenantContext tenant, ICurrentUser currentUser,
    Desk.Application.Authorization.IEffectivePermissionService permissions) : IRoleAdminService
{
    private static readonly RoleType[] StaffRoleTypes =
        [RoleType.MspAdministrator, RoleType.Manager, RoleType.Technician, RoleType.Auditor];

    public IReadOnlyList<PermissionDefinitionDto> Catalog()
        => PermissionCatalog.Definitions
            .Select(d => new PermissionDefinitionDto(
                d.Key, d.Module, d.DisplayName, d.SupportedScopes.ToList(), d.DefaultScope, d.IsBoardAware))
            .ToList();

    public async Task<IReadOnlyList<RoleDetailDto>> ListAsync(CancellationToken ct = default)
    {
        var roles = await VisibleRoles().AsNoTracking()
            .Include(r => r.Permissions)
            .OrderBy(r => r.IsSystemRole ? 0 : 1).ThenBy(r => r.BuiltInType).ThenBy(r => r.Name)
            .ToListAsync(ct);

        var roleIds = roles.Select(r => r.Id).ToList();
        // Counts scoped to THIS tenant's users: a built-in role is shared across tenants, and the
        // number that matters here is how many of OUR people hold it.
        var counts = await db.UserRoles.AsNoTracking()
            .Where(ur => roleIds.Contains(ur.RoleId))
            .Join(db.AppUsers.AsNoTracking().Where(u => u.MspOrganizationId == tenant.OrganizationId),
                ur => ur.AppUserId, u => u.Id, (ur, _) => ur.RoleId)
            .GroupBy(id => id).Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        var held = currentUser.UserId is { } uid
            ? (await db.UserRoles.AsNoTracking().Where(ur => ur.AppUserId == uid)
                .Select(ur => ur.RoleId).ToListAsync(ct)).ToHashSet()
            : [];

        return roles.Select(r => ToDetail(r, counts.GetValueOrDefault(r.Id), held.Contains(r.Id))).ToList();
    }

    public async Task<RoleDetailDto> CreateAsync(SaveRoleInput input, CancellationToken ct = default)
    {
        var (name, grants) = await ValidateAsync(input, excludeRoleId: null, ct);

        var role = new Role { MspOrganizationId = tenant.OrganizationId, Name = name, IsSystemRole = false };
        foreach (var g in grants)
            role.Permissions.Add(new RolePermission { PermissionKey = g.PermissionKey, Scope = g.Scope });
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("role.created", "Role", role.Id.ToString(),
            new { name, grants = grants.Select(g => $"{g.PermissionKey}:{g.Scope}") }, ct);
        return ToDetail(role, userCount: 0, heldByCaller: false);
    }

    public async Task<RoleDetailDto> UpdateAsync(Guid roleId, SaveRoleInput input, CancellationToken ct = default)
    {
        var role = await LoadCustomAsync(roleId, "edited", ct);

        // Same self-escalation boundary as the per-user guards: the caller genuinely holds
        // RolesManage, but widening a role they HOLD widens their own access. Someone else must do it.
        if (currentUser.UserId is { } uid &&
            await db.UserRoles.AnyAsync(ur => ur.AppUserId == uid && ur.RoleId == roleId, ct))
            throw new ForbiddenException("You cannot edit a role you hold. Ask another administrator.");

        var (name, grants) = await ValidateAsync(input, excludeRoleId: roleId, ct);

        role.Name = name;
        db.Set<RolePermission>().RemoveRange(role.Permissions);
        role.Permissions.Clear();
        foreach (var g in grants)
            db.Set<RolePermission>().Add(new RolePermission { RoleId = role.Id, PermissionKey = g.PermissionKey, Scope = g.Scope });
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("role.updated", "Role", roleId.ToString(),
            new { name, grants = grants.Select(g => $"{g.PermissionKey}:{g.Scope}") }, ct);

        var userCount = await CountTenantHoldersAsync(roleId, ct);
        return new RoleDetailDto(role.Id, role.Name, false, null, userCount, false,
            grants.Select(g => new RoleGrantDto(g.PermissionKey, g.Scope)).ToList());
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await LoadCustomAsync(roleId, "deleted", ct);

        var holders = await CountTenantHoldersAsync(roleId, ct);
        if (holders > 0)
            throw new ValidationFailedException(
                $"{holders} user{(holders == 1 ? "" : "s")} still hold{(holders == 1 ? "s" : "")} this role — remove it from them first.");

        var name = role.Name;
        db.Set<RolePermission>().RemoveRange(role.Permissions);
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("role.deleted", "Role", roleId.ToString(), new { name }, ct);
    }

    public async Task<IReadOnlyList<UserEffectivePermissionDto>> HoldersAsync(string permissionKey, CancellationToken ct = default)
    {
        if (!PermissionCatalog.TryGet(permissionKey, out _))
            throw new ValidationFailedException($"'{permissionKey}' is not a known permission.");

        var users = await db.AppUsers.AsNoTracking()
            .Where(u => u.MspOrganizationId == tenant.OrganizationId)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();

        // Which of each user's roles grant this key at all — the "via" column. The RESOLVED answer
        // below still comes from the engine; this only names the contributing roles.
        var viaRoles = (await db.UserRoles.AsNoTracking()
                .Where(ur => userIds.Contains(ur.AppUserId))
                .Join(db.Roles.AsNoTracking().Where(r => r.Permissions.Any(p => p.PermissionKey == permissionKey)),
                    ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.AppUserId, r.Name })
                .ToListAsync(ct))
            .GroupBy(x => x.AppUserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).Order().ToList());

        var result = new List<UserEffectivePermissionDto>(users.Count);
        foreach (var u in users)
        {
            // Per-user resolution, not a hand-rolled bulk query: the engine is the single place the
            // union/override rules live, and this screen exists to show ITS answer.
            var eff = await permissions.ResolveAsync(u.Id, permissionKey, ct);
            result.Add(new UserEffectivePermissionDto(
                u.Id, u.DisplayName, u.Email, u.PhotoUrl, u.IsActive,
                eff.Scope, eff.Source.ToString(), eff.BoardMode.ToString(),
                viaRoles.GetValueOrDefault(u.Id, [])));
        }
        return result;
    }

    /// <summary>The staff built-ins plus this tenant's custom roles — nothing else exists here.</summary>
    private IQueryable<Role> VisibleRoles()
        => db.Roles.Where(r =>
            (r.IsSystemRole && r.BuiltInType != null && StaffRoleTypes.Contains(r.BuiltInType.Value))
            || (!r.IsSystemRole && r.MspOrganizationId == tenant.OrganizationId));

    private async Task<Role> LoadCustomAsync(Guid roleId, string verb, CancellationToken ct)
    {
        var role = await VisibleRoles().Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new NotFoundException("Role");
        if (role.IsSystemRole)
            throw new ValidationFailedException($"Built-in roles cannot be {verb} — create a custom role instead.");
        return role;
    }

    private async Task<int> CountTenantHoldersAsync(Guid roleId, CancellationToken ct)
        => await db.UserRoles.AsNoTracking().Where(ur => ur.RoleId == roleId)
            .Join(db.AppUsers.AsNoTracking().Where(u => u.MspOrganizationId == tenant.OrganizationId),
                ur => ur.AppUserId, u => u.Id, (ur, _) => 1)
            .CountAsync(ct);

    private async Task<(string Name, IReadOnlyList<RoleGrantDto> Grants)> ValidateAsync(
        SaveRoleInput input, Guid? excludeRoleId, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (name.Length is < 2 or > 80)
            throw new ValidationFailedException("Role name must be between 2 and 80 characters.");

        var taken = await VisibleRoles().AnyAsync(
            r => r.Id != excludeRoleId && r.Name.ToLower() == name.ToLower(), ct);
        if (taken)
            throw new ValidationFailedException("A role with that name already exists.");

        var grants = input.Grants ?? [];
        if (grants.Count == 0)
            throw new ValidationFailedException("Grant at least one permission — a role with none can do nothing.");
        if (grants.Select(g => g.PermissionKey).Distinct(StringComparer.Ordinal).Count() != grants.Count)
            throw new ValidationFailedException("Each permission may appear only once.");

        foreach (var g in grants)
        {
            if (!PermissionCatalog.TryGet(g.PermissionKey, out var def) || def is null)
                throw new ValidationFailedException($"'{g.PermissionKey}' is not a known permission.");
            // Only scopes enforcement can honour: offering Department on a key resolved as
            // all-or-nothing would be a grant the UI promises and the engine ignores.
            if (!def.SupportedScopes.Contains(g.Scope))
                throw new ValidationFailedException(
                    $"'{def.DisplayName}' does not support the {g.Scope} scope.");
        }

        return (name, grants);
    }

    private static RoleDetailDto ToDetail(Role r, int userCount, bool heldByCaller)
        => new(r.Id, r.Name, r.IsSystemRole, r.BuiltInType, userCount, heldByCaller,
            r.Permissions.OrderBy(p => p.PermissionKey)
                .Select(p => new RoleGrantDto(p.PermissionKey, p.Scope)).ToList());
}
