using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Organization;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Phase 1 of the RBAC expansion: new domain entities, the RolePermission.Scope column, and the
/// INullableTenantScoped filter fix for PermissionTemplate/AuditLogEntry. Nothing here enforces
/// scope yet — these tests only prove the model is sound and the migration is a genuine no-op.
/// </summary>
public class OrganizationAndAuthorizationModelTests
{
    private static readonly Guid OrgA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrgB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Departments_are_tenant_isolated()
    {
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.Departments.Add(new Department { MspOrganizationId = OrgA, Name = "IT Support" });
            seed.Departments.Add(new Department { MspOrganizationId = OrgB, Name = "IT Support" });
            await seed.SaveChangesAsync();
        }

        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);
        var visible = await asA.Departments.ToListAsync();

        visible.Should().ContainSingle();
        visible[0].MspOrganizationId.Should().Be(OrgA);
    }

    [Fact]
    public async Task A_department_name_can_repeat_across_tenants_but_not_within_one()
    {
        // The unique index is (MspOrganizationId, Name) — same name, different tenants, is fine;
        // the in-memory provider doesn't enforce unique indexes, so this only proves the cross-
        // tenant half is unblocked at the model level. The within-tenant half is a Postgres-level
        // constraint, verified separately against a real database (see Phase 1 verification notes).
        var db = Guid.NewGuid().ToString();
        await using var seed = TestDbContextFactory.ForPlatform(db);
        seed.Departments.Add(new Department { MspOrganizationId = OrgA, Name = "Billing" });
        seed.Departments.Add(new Department { MspOrganizationId = OrgB, Name = "Billing" });

        var act = async () => await seed.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Team_belongs_to_its_department_and_is_tenant_isolated()
    {
        var db = Guid.NewGuid().ToString();
        var deptId = Guid.NewGuid();
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.Departments.Add(new Department { Id = deptId, MspOrganizationId = OrgA, Name = "IT Support" });
            seed.Teams.Add(new Team { DepartmentId = deptId, MspOrganizationId = OrgA, Name = "Level 1" });
            await seed.SaveChangesAsync();
        }

        await using var asB = TestDbContextFactory.ForTenant(db, OrgB);
        (await asB.Teams.ToListAsync()).Should().BeEmpty();

        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);
        (await asA.Teams.ToListAsync()).Should().ContainSingle(t => t.Name == "Level 1");
    }

    [Fact]
    public void User_board_access_defaults_to_all_when_no_row_exists()
    {
        // No enforcement reads this yet, but the model default itself must be All — a fail-open
        // default is the only safe one for a dimension nothing has granted anyone through yet.
        new UserBoardAccess { AppUserId = Guid.NewGuid() }.Mode.Should().Be(BoardAccessMode.All);
    }

    [Fact]
    public void Role_permission_defaults_to_all_scope()
    {
        // Every existing role/permission pair must read as unscoped until a later phase's
        // migration deliberately sets otherwise — this is what makes the schema change inert.
        new RolePermission { PermissionKey = Permissions.TicketsUpdate }.Scope.Should().Be(PermissionScope.All);
    }

    [Fact]
    public async Task Permission_override_replaces_rather_than_needing_a_role_lookup_to_interpret()
    {
        var db = Guid.NewGuid().ToString();
        await using var ctx = TestDbContextFactory.ForTenant(db, OrgA);
        var userId = Guid.NewGuid();
        ctx.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            AppUserId = userId,
            PermissionKey = Permissions.TicketsUpdate,
            Effect = PermissionEffect.Deny,
        });
        await ctx.SaveChangesAsync();

        var stored = await ctx.UserPermissionOverrides.SingleAsync(o => o.AppUserId == userId);
        stored.Effect.Should().Be(PermissionEffect.Deny);
        stored.Scope.Should().BeNull(); // meaningless for a Deny — never set
    }

    [Fact]
    public async Task Built_in_permission_template_is_visible_from_every_tenant()
    {
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.PermissionTemplates.Add(new PermissionTemplate
            {
                MspOrganizationId = null, Name = "Full Administrator",
                BaseRoleType = RoleType.MspAdministrator, IsSystemTemplate = true,
            });
            await seed.SaveChangesAsync();
        }

        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);
        await using var asB = TestDbContextFactory.ForTenant(db, OrgB);

        (await asA.PermissionTemplates.SingleAsync()).Name.Should().Be("Full Administrator");
        (await asB.PermissionTemplates.SingleAsync()).Name.Should().Be("Full Administrator");
    }

    [Fact]
    public async Task A_tenant_owned_permission_template_is_isolated_from_other_tenants()
    {
        // PermissionTemplate is INullableTenantScoped, not ITenantScoped — it is not auto-stamped
        // on write (see DeskDbContext.ApplyInvariants), so a caller creating a tenant-owned row
        // must set MspOrganizationId itself, exactly as AuditWriter already does for AuditLogEntry.
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForTenant(db, OrgA))
        {
            seed.PermissionTemplates.Add(new PermissionTemplate
            {
                MspOrganizationId = OrgA, Name = "Acme Custom Role", BaseRoleType = RoleType.Technician,
            });
            await seed.SaveChangesAsync();
        }

        await using var asB = TestDbContextFactory.ForTenant(db, OrgB);
        (await asB.PermissionTemplates.ToListAsync()).Should().BeEmpty();

        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);
        (await asA.PermissionTemplates.ToListAsync()).Should().ContainSingle(t => t.Name == "Acme Custom Role");
    }

    [Fact]
    public async Task A_tenant_owned_audit_entry_is_isolated_from_other_tenants()
    {
        // AuditLogEntry already had this behavior enforced by hand in AuditQueryService; this
        // proves the automatic filter now does the same at the DbContext level.
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForTenant(db, OrgA))
        {
            seed.AuditLog.Add(new Desk.Domain.Audit.AuditLogEntry
            {
                MspOrganizationId = OrgA, Action = "user.created", EntityType = "AppUser",
            });
            await seed.SaveChangesAsync();
        }

        await using var asB = TestDbContextFactory.ForTenant(db, OrgB);
        (await asB.AuditLog.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_platform_level_audit_entry_with_no_org_is_visible_from_every_tenant()
    {
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.AuditLog.Add(new Desk.Domain.Audit.AuditLogEntry
            {
                MspOrganizationId = null, Action = "platform.something", EntityType = "System",
            });
            await seed.SaveChangesAsync();
        }

        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);
        (await asA.AuditLog.ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task AppUser_lookup_by_subject_still_works_with_no_tenant_scope_established()
    {
        // The exact scenario DeskClaimsTransformation depends on: looking up an AppUser BEFORE any
        // tenant scope exists. AppUser was deliberately excluded from the nullable-tenant filter —
        // this test is the regression guard for that decision.
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.AppUsers.Add(new AppUser
            {
                MspOrganizationId = OrgA, Email = "tech@acme.test", DisplayName = "Tech",
                IdpSubject = "kc-sub-123", IsActive = true,
            });
            await seed.SaveChangesAsync();
        }

        await using var unscoped = TestDbContextFactory.Unscoped(db);
        var found = await unscoped.AppUsers.SingleOrDefaultAsync(u => u.IdpSubject == "kc-sub-123" && u.IsActive);

        found.Should().NotBeNull();
    }

    [Fact]
    public async Task Seeding_departments_twice_does_not_duplicate_them()
    {
        var db = Guid.NewGuid().ToString();
        await using var ctx = TestDbContextFactory.ForPlatform(db);
        ctx.MspOrganizations.Add(new MspOrganization { Id = OrgA, Name = "Acme", Slug = "acme-" + Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        await DatabaseSeeder.SeedDefaultDepartmentsAsync(ctx);
        await DatabaseSeeder.SeedDefaultDepartmentsAsync(ctx);

        var departments = await ctx.Departments.Where(d => d.MspOrganizationId == OrgA).ToListAsync();
        departments.Should().HaveCount(7);
    }

    [Fact]
    public async Task Seeding_departments_leaves_an_org_alone_once_it_has_any_department()
    {
        // An admin who deleted 6 of the 7 defaults down to 1 must not have the other 6 resurrected
        // on the next deploy — "already has any departments" is the whole idempotency condition.
        var db = Guid.NewGuid().ToString();
        await using var ctx = TestDbContextFactory.ForPlatform(db);
        ctx.MspOrganizations.Add(new MspOrganization { Id = OrgA, Name = "Acme", Slug = "acme-" + Guid.NewGuid() });
        ctx.Departments.Add(new Department { MspOrganizationId = OrgA, Name = "Only One Left" });
        await ctx.SaveChangesAsync();

        await DatabaseSeeder.SeedDefaultDepartmentsAsync(ctx);

        var departments = await ctx.Departments.Where(d => d.MspOrganizationId == OrgA).ToListAsync();
        departments.Should().ContainSingle().Which.Name.Should().Be("Only One Left");
    }

    [Fact]
    public async Task Reseeding_backfills_scope_onto_rows_created_before_the_column_existed()
    {
        // Every deployed database seeded its role permissions before Scope existed, so they all sit
        // at the column default (All). The seeder must correct the handful whose role genuinely
        // reaches less far, or a deployed database would silently grant more than a fresh one.
        var db = Guid.NewGuid().ToString();
        await using var ctx = TestDbContextFactory.ForPlatform(db);
        await DatabaseSeeder.SeedBuiltInRolesAsync(ctx);

        var technician = await ctx.Roles.IgnoreQueryFilters().Include(r => r.Permissions)
            .SingleAsync(r => r.BuiltInType == RoleType.Technician);
        var viewAssigned = technician.Permissions.Single(p => p.PermissionKey == Permissions.TicketsViewAssigned);
        viewAssigned.Scope = PermissionScope.All;   // simulate the pre-column state
        await ctx.SaveChangesAsync();

        await DatabaseSeeder.SeedBuiltInRolesAsync(ctx);

        var corrected = await ctx.Set<RolePermission>()
            .SingleAsync(p => p.RoleId == technician.Id && p.PermissionKey == Permissions.TicketsViewAssigned);
        corrected.Scope.Should().Be(PermissionScope.Assigned);
    }

    [Fact]
    public async Task Reseeding_never_widens_a_scope_an_admin_deliberately_narrowed()
    {
        // The backfill only ever touches rows still sitting at the default. A row an admin has
        // deliberately narrowed must survive a redeploy untouched — otherwise every deploy would
        // silently undo their access decisions.
        var db = Guid.NewGuid().ToString();
        await using var ctx = TestDbContextFactory.ForPlatform(db);
        await DatabaseSeeder.SeedBuiltInRolesAsync(ctx);

        var manager = await ctx.Roles.IgnoreQueryFilters().Include(r => r.Permissions)
            .SingleAsync(r => r.BuiltInType == RoleType.Manager);
        var update = manager.Permissions.Single(p => p.PermissionKey == Permissions.TicketsUpdate);
        update.Scope = PermissionScope.Department;   // an admin's deliberate narrowing
        await ctx.SaveChangesAsync();

        await DatabaseSeeder.SeedBuiltInRolesAsync(ctx);

        var after = await ctx.Set<RolePermission>()
            .SingleAsync(p => p.RoleId == manager.Id && p.PermissionKey == Permissions.TicketsUpdate);
        after.Scope.Should().Be(PermissionScope.Department);
    }

    [Fact]
    public async Task Seeding_permission_templates_twice_does_not_duplicate_them()
    {
        var db = Guid.NewGuid().ToString();
        await using var ctx = TestDbContextFactory.ForPlatform(db);

        await DatabaseSeeder.SeedBuiltInPermissionTemplatesAsync(ctx);
        await DatabaseSeeder.SeedBuiltInPermissionTemplatesAsync(ctx);

        (await ctx.PermissionTemplates.CountAsync(t => t.IsSystemTemplate)).Should().Be(8);
    }
}
