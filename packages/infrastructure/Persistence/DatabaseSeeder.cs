using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Persistence;

/// <summary>Seeds the seven built-in system roles with their default permission claim sets.</summary>
public static class DatabaseSeeder
{
    public static async Task SeedBuiltInRolesAsync(DeskDbContext db, CancellationToken ct = default)
    {
        foreach (var roleType in Enum.GetValues<RoleType>())
        {
            var exists = await db.Roles
                .IgnoreQueryFilters()
                .AnyAsync(r => r.IsSystemRole && r.BuiltInType == roleType, ct);
            if (exists) continue;

            var role = new Role
            {
                Name = roleType.ToString(),
                BuiltInType = roleType,
                IsSystemRole = true,
                MspOrganizationId = null,
            };
            foreach (var perm in Permissions.ForRole(roleType))
                role.Permissions.Add(new RolePermission { PermissionKey = perm });

            db.Roles.Add(role);
        }

        await db.SaveChangesAsync(ct);
    }
}
