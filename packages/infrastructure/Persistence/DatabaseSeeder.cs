using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Organization;
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
            var existing = await db.Roles
                .IgnoreQueryFilters()
                .Include(r => r.Permissions)
                .SingleOrDefaultAsync(r => r.IsSystemRole && r.BuiltInType == roleType, ct);

            // A role seeded by an earlier version predates any permission added since. Skipping it
            // outright meant a new claim reached fresh installs only and silently never reached a
            // deployed one, where the feature would then be invisible with no error to explain it.
            // Additive only: a claim removed from the catalogue is left alone rather than revoked
            // underneath whoever is relying on it.
            if (existing is not null)
            {
                var held = existing.Permissions.Select(p => p.PermissionKey).ToHashSet();
                foreach (var missing in Permissions.ForRole(roleType).Where(p => !held.Contains(p)))
                {
                    // Added explicitly rather than through existing.Permissions: BaseEntity assigns
                    // its own Id, so a child discovered on a tracked parent looks to EF like a row
                    // that already exists and is tracked Modified — which fails the save outright,
                    // taking down startup on the very deployments this is meant to repair.
                    db.Set<RolePermission>().Add(new RolePermission
                    {
                        RoleId = existing.Id,
                        PermissionKey = missing,
                    });
                }
                continue;
            }

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

    /// <summary>The 7 default departments seeded for every organization, staff org structure only
    /// (unrelated to client companies).</summary>
    private static readonly string[] DefaultDepartmentNames =
        ["IT Support", "NOC", "Projects", "Sales", "Billing", "Administration", "Security"];

    /// <summary>
    /// Seeds the default department set for every organization that does not yet have any
    /// departments. Runs under platform scope so it can see every tenant; per-org writes still go
    /// through the normal tenant-stamping path since <see cref="Department"/> is tenant-scoped.
    /// Idempotent — an organization that already has ANY departments (including ones an admin has
    /// since renamed or deleted down to a subset) is left alone entirely, so this never resurrects
    /// a department someone deliberately removed.
    /// </summary>
    public static async Task SeedDefaultDepartmentsAsync(DeskDbContext db, CancellationToken ct = default)
    {
        var orgIds = await db.MspOrganizations.IgnoreQueryFilters().Select(o => o.Id).ToListAsync(ct);
        var orgsWithDepartments = await db.Departments.IgnoreQueryFilters()
            .Select(d => d.MspOrganizationId).Distinct().ToListAsync(ct);
        var missing = orgIds.Except(orgsWithDepartments);

        foreach (var orgId in missing)
        {
            for (var i = 0; i < DefaultDepartmentNames.Length; i++)
            {
                db.Departments.Add(new Department
                {
                    MspOrganizationId = orgId,
                    Name = DefaultDepartmentNames[i],
                    IsSystemDefault = true,
                    SortOrder = i,
                });
            }
        }

        if (missing.Any())
            await db.SaveChangesAsync(ct);
    }

    /// <summary>The 8 built-in permission templates. Entries are populated in a later phase once
    /// scoped enforcement exists to give them something meaningful to grant — the rows exist now so
    /// they have stable ids a later Add-User flow can reference.</summary>
    private static readonly (string Name, string Description, RoleType BaseRole)[] BuiltInTemplates =
    [
        ("Full Administrator", "Unrestricted access across the organization.", RoleType.MspAdministrator),
        ("Service Desk Manager", "Manages the service desk team and its tickets.", RoleType.Manager),
        ("Senior Technician", "Broader ticket access than a standard technician.", RoleType.Technician),
        ("Standard Technician", "Assigned-ticket access for day-to-day work.", RoleType.Technician),
        ("Dispatcher", "Assigns and routes incoming tickets.", RoleType.Technician),
        ("Billing User", "Access to billing and invoicing data.", RoleType.Technician),
        ("Auditor", "Read-only access to audit and security data.", RoleType.Auditor),
        ("Read Only", "View access with no ability to change anything.", RoleType.Technician),
    ];

    public static async Task SeedBuiltInPermissionTemplatesAsync(DeskDbContext db, CancellationToken ct = default)
    {
        var existingNames = await db.PermissionTemplates.IgnoreQueryFilters()
            .Where(t => t.IsSystemTemplate).Select(t => t.Name).ToListAsync(ct);

        foreach (var (name, description, baseRole) in BuiltInTemplates)
        {
            if (existingNames.Contains(name)) continue;
            db.PermissionTemplates.Add(new PermissionTemplate
            {
                MspOrganizationId = null,
                Name = name,
                Description = description,
                BaseRoleType = baseRole,
                IsSystemTemplate = true,
            });
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
