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
                var held = existing.Permissions.ToDictionary(p => p.PermissionKey);
                foreach (var (key, scope) in Permissions.ForRole(roleType))
                {
                    if (held.TryGetValue(key, out var row))
                    {
                        // Backfill scope on rows seeded before the Scope column existed. They all
                        // landed on the column default (All); this corrects the handful whose role
                        // genuinely reaches less far, so a deployed database matches a fresh one.
                        // Only ever narrows a default — never widens an admin's deliberate choice,
                        // because the only rows this touches are ones still sitting at the default.
                        if (row.Scope == PermissionScope.All && scope != PermissionScope.All)
                            row.Scope = scope;
                        continue;
                    }

                    // Added explicitly rather than through existing.Permissions: BaseEntity assigns
                    // its own Id, so a child discovered on a tracked parent looks to EF like a row
                    // that already exists and is tracked Modified — which fails the save outright,
                    // taking down startup on the very deployments this is meant to repair.
                    db.Set<RolePermission>().Add(new RolePermission
                    {
                        RoleId = existing.Id,
                        PermissionKey = key,
                        Scope = scope,
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
            foreach (var (key, scope) in Permissions.ForRole(roleType))
                role.Permissions.Add(new RolePermission { PermissionKey = key, Scope = scope });

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

/// <summary>
    /// The 8 built-in permission templates. Applying one assigns BaseRole (which already grants
    /// that role's own permissions) THEN materializes these entries as overrides on top — so a
    /// template's entries are only the DIFFERENCE from its base role, not a full restatement of it.
    /// "Standard Technician" and "Auditor" have none at all for exactly that reason: they ARE their
    /// base role, unmodified, and exist as named choices for clarity in the picker rather than
    /// because they change anything.
    ///
    /// Billing has no dedicated permission module yet (that is future work, not part of this
    /// batch) — "Billing User" narrows ticket access instead of granting anything billing-specific,
    /// which is the honest thing to do with the module that actually exists today.
    /// </summary>
    private static readonly (string Name, string Description, RoleType BaseRole, (string Key, PermissionEffect Effect, PermissionScope? Scope)[] Entries)[] BuiltInTemplates =
    [
        ("Full Administrator", "Unrestricted access across the organization.", RoleType.MspAdministrator, []),
        ("Service Desk Manager", "Manages the service desk team and its tickets.", RoleType.Manager,
        [
            (Permissions.ProductivityViewOwn, PermissionEffect.Grant, PermissionScope.Own),
        ]),
        ("Senior Technician", "Broader ticket access than a standard technician.", RoleType.Technician,
        [
            (Permissions.TicketsViewAll, PermissionEffect.Grant, PermissionScope.Department),
        ]),
        ("Standard Technician", "Assigned-ticket access for day-to-day work.", RoleType.Technician, []),
        ("Dispatcher", "Assigns and routes incoming tickets.", RoleType.Technician,
        [
            (Permissions.TicketsViewAll, PermissionEffect.Grant, PermissionScope.Department),
            (Permissions.TicketsUpdate, PermissionEffect.Grant, PermissionScope.Department),
        ]),
        ("Billing User", "Ticket access narrowed for someone who only needs reporting.", RoleType.Technician,
        [
            (Permissions.TicketsUpdate, PermissionEffect.Deny, null),
            (Permissions.TicketsAddPublicNote, PermissionEffect.Deny, null),
            (Permissions.TicketsLogTime, PermissionEffect.Deny, null),
            (Permissions.ReportsView, PermissionEffect.Grant, PermissionScope.All),
        ]),
        ("Auditor", "Read-only access to audit and security data.", RoleType.Auditor, []),
        ("Read Only", "View access with no ability to change anything.", RoleType.Technician,
        [
            (Permissions.TicketsUpdate, PermissionEffect.Deny, null),
            (Permissions.TicketsAddPublicNote, PermissionEffect.Deny, null),
            (Permissions.TicketsLogTime, PermissionEffect.Deny, null),
        ]),
    ];

    public static async Task SeedBuiltInPermissionTemplatesAsync(DeskDbContext db, CancellationToken ct = default)
    {
        var existing = await db.PermissionTemplates.IgnoreQueryFilters()
            .Where(t => t.IsSystemTemplate)
            .Include(t => t.Entries)
            .ToListAsync(ct);

        foreach (var (name, description, baseRole, entries) in BuiltInTemplates)
        {
            var template = existing.FirstOrDefault(t => t.Name == name);
            if (template is null)
            {
                template = new PermissionTemplate
                {
                    MspOrganizationId = null, Name = name, Description = description,
                    BaseRoleType = baseRole, IsSystemTemplate = true,
                };
                db.PermissionTemplates.Add(template);
            }

            // Backfill entries onto a template that was already seeded empty (every deployed
            // database, until this change) — additive only, so a since-edited entry isn't reset.
            var heldKeys = template.Entries.Select(e => e.PermissionKey).ToHashSet();
            foreach (var (key, effect, scope) in entries)
            {
                if (heldKeys.Contains(key)) continue;
                db.Set<PermissionTemplateEntry>().Add(new PermissionTemplateEntry
                {
                    PermissionTemplateId = template.Id, PermissionKey = key, Effect = effect, Scope = scope,
                });
            }
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
