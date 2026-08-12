using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Tenancy;
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

    /// <summary>
    /// Production bootstrap: the organization and its first MSP administrator, from configuration.
    /// Without this a fresh deployment is a locked room — sign-in binds an IdP subject to an
    /// EXISTING AppUser by email, so with zero rows nobody can ever get in. The admin is created
    /// unlinked and binds on their first Keycloak login, exactly like an invited technician.
    /// Idempotent: an existing user with the email (any casing) means nothing is written.
    /// </summary>
    public static async Task SeedBootstrapAdminAsync(
        DeskDbContext db, string organizationName, string organizationSlug,
        string adminEmail, string adminName, CancellationToken ct = default)
    {
        var email = adminEmail.Trim();
        var exists = await db.AppUsers.IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), ct);
        if (exists) return;

        var org = await db.MspOrganizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Slug == organizationSlug, ct);
        if (org is null)
        {
            org = new MspOrganization { Name = organizationName, Slug = organizationSlug };
            db.MspOrganizations.Add(org);
            await db.SaveChangesAsync(ct);
        }

        var user = new AppUser
        {
            MspOrganizationId = org.Id,
            Email = email,
            DisplayName = adminName.Trim(),
            IdpSubject = null, // binds on first IdP login by verified email
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);

        var mspAdmin = await db.Roles.IgnoreQueryFilters()
            .FirstAsync(r => r.IsSystemRole && r.BuiltInType == RoleType.MspAdministrator, ct);
        db.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = mspAdmin.Id });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Local-mode subject the dev auto-login authenticates as.</summary>
    public const string DevAdminSubject = "dev-admin";

    /// <summary>
    /// Local/demo bootstrap: a demo MSP organization and an MSP-administrator user tied to the dev
    /// auto-login subject, so the platform has a working tenant + admin without Keycloak. NO business
    /// content is seeded — connections, companies, tickets and mappings all come from configuring a
    /// real PSA connection and running a sync. Only invoked in local mode.
    /// </summary>
    public static async Task SeedLocalDemoAsync(DeskDbContext db, CancellationToken ct = default)
    {
        var org = await db.MspOrganizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == "demo", ct);
        if (org is null)
        {
            org = new MspOrganization { Name = "Demo MSP", Slug = "demo" };
            db.MspOrganizations.Add(org);
            await db.SaveChangesAsync(ct);
        }

        var hasUser = await db.AppUsers.IgnoreQueryFilters().AnyAsync(u => u.IdpSubject == DevAdminSubject, ct);
        if (!hasUser)
        {
            var user = new AppUser
            {
                MspOrganizationId = org.Id,
                Email = "dev-admin@local",
                DisplayName = "Demo Admin",
                IdpSubject = DevAdminSubject,
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync(ct);

            var mspAdmin = await db.Roles.IgnoreQueryFilters()
                .FirstAsync(r => r.IsSystemRole && r.BuiltInType == RoleType.MspAdministrator, ct);
            db.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = mspAdmin.Id });
            await db.SaveChangesAsync(ct);
        }
    }
}
