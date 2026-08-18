using Desk.Application.Admin;
using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Organization;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The Users & Access Management backend surface: search/filter/pagination, edit, delete-cascade,
/// department/team/board assignment, permission templates, effective permissions, and bulk actions.
/// </summary>
public class UserAdminExpandedTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static async Task<(AdminHarness H, UserAdminService Svc, Dictionary<RoleType, Guid> Roles)> SetupAsync()
    {
        var h = AdminHarness.Create(Org);
        var roles = new Dictionary<RoleType, Guid>();
        foreach (var t in new[] { RoleType.MspAdministrator, RoleType.Manager, RoleType.Technician, RoleType.Auditor })
        {
            var role = new Desk.Domain.Identity.Role { Name = t.ToString(), BuiltInType = t, IsSystemRole = true };
            h.Db.Roles.Add(role);
            roles[t] = role.Id;
        }
        await h.Db.SaveChangesAsync();
        var svc = new UserAdminService(h.Db, new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), h.Tenant, h.User,
            new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock), new EffectivePermissionService(h.Db), h.Clock);
        return (h, svc, roles);
    }

    // ---- Search / filter / pagination / summary ----

    [Fact]
    public async Task Search_matches_name_or_email_case_insensitively()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        await svc.CreateAsync(new CreateStaffUserInput("Jane Smith", "jane@msp.test", [roles[RoleType.Technician]]));
        await svc.CreateAsync(new CreateStaffUserInput("Bob Jones", "bob@msp.test", [roles[RoleType.Technician]]));

        var result = await svc.ListAsync(new UserListQuery(Search: "JANE"));

        result.Users.Should().ContainSingle().Which.DisplayName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task Role_filter_narrows_to_holders_of_that_role_only()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        await svc.CreateAsync(new CreateStaffUserInput("Admin Amy", "amy@msp.test", [roles[RoleType.MspAdministrator]]));
        await svc.CreateAsync(new CreateStaffUserInput("Tech Tom", "tom@msp.test", [roles[RoleType.Technician]]));

        var result = await svc.ListAsync(new UserListQuery(RoleId: roles[RoleType.MspAdministrator]));

        result.Users.Should().ContainSingle().Which.DisplayName.Should().Be("Admin Amy");
    }

    [Fact]
    public async Task Summary_counts_describe_the_whole_org_not_the_filtered_result()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        await svc.CreateAsync(new CreateStaffUserInput("Admin Amy", "amy2@msp.test", [roles[RoleType.MspAdministrator]]));
        var tom = await svc.CreateAsync(new CreateStaffUserInput("Tech Tom", "tom2@msp.test", [roles[RoleType.Technician]]));
        await svc.SetActiveAsync(tom.Id, active: false);

        // Filtered down to zero matches — the summary must still reflect the real org totals.
        var result = await svc.ListAsync(new UserListQuery(Search: "nobody-matches-this"));

        result.Users.Should().BeEmpty();
        result.Summary.Total.Should().Be(2);
        result.Summary.Active.Should().Be(1);
        result.Summary.Pending.Should().Be(2); // neither has signed in yet
        result.Summary.Administrators.Should().Be(1);
    }

    [Fact]
    public async Task Pagination_returns_the_requested_page_and_the_true_total()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        for (var i = 0; i < 5; i++)
            await svc.CreateAsync(new CreateStaffUserInput($"User {i}", $"user{i}@msp.test", [roles[RoleType.Technician]]));

        var page1 = await svc.ListAsync(new UserListQuery(Page: 1, PageSize: 2));
        var page2 = await svc.ListAsync(new UserListQuery(Page: 2, PageSize: 2));

        page1.Users.Should().HaveCount(2);
        page2.Users.Should().HaveCount(2);
        page1.TotalMatching.Should().Be(5);
        page1.Users.Select(u => u.Id).Should().NotIntersectWith(page2.Users.Select(u => u.Id));
    }

    // ---- Update ----

    [Fact]
    public async Task Update_changes_profile_fields_and_is_audited()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane3@msp.test", [roles[RoleType.Technician]]));

        var updated = await svc.UpdateAsync(created.Id,
            new UpdateStaffUserInput("Jane Updated", "jane3@msp.test", "+1 555 0100", "Toronto", null));

        updated.DisplayName.Should().Be("Jane Updated");
        updated.PhoneNumber.Should().Be("+1 555 0100");
        updated.Location.Should().Be("Toronto");
        (await h.Db.AuditLog.AnyAsync(a => a.Action == "user.updated" && a.EntityId == created.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task A_user_cannot_be_set_as_their_own_manager()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane4@msp.test", [roles[RoleType.Technician]]));

        var act = () => svc.UpdateAsync(created.Id,
            new UpdateStaffUserInput("Jane", "jane4@msp.test", null, null, created.Id));

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_removes_the_user_and_every_row_that_references_them()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane5@msp.test", [roles[RoleType.Technician]]));
        var dept = new Department { MspOrganizationId = Org, Name = "IT Support" };
        h.Db.Departments.Add(dept);
        await h.Db.SaveChangesAsync();
        await svc.SetDepartmentAsync(created.Id, dept.Id, isPrimary: true);
        await svc.SetBoardAccessModeAsync(created.Id, BoardAccessMode.Selected);

        await svc.DeleteAsync(created.Id);

        (await h.Db.AppUsers.AnyAsync(u => u.Id == created.Id)).Should().BeFalse();
        (await h.Db.UserRoles.AnyAsync(r => r.AppUserId == created.Id)).Should().BeFalse();
        (await h.Db.UserDepartments.AnyAsync(d => d.AppUserId == created.Id)).Should().BeFalse();
        (await h.Db.UserBoardAccesses.AnyAsync(a => a.AppUserId == created.Id)).Should().BeFalse();
        (await h.Db.AuditLog.AnyAsync(a => a.Action == "user.deleted" && a.EntityId == created.Id.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_manager_clears_the_reference_on_their_reports_rather_than_failing()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var manager = await svc.CreateAsync(new CreateStaffUserInput("Manager Mo", "mo@msp.test", [roles[RoleType.Manager]]));
        var report = await svc.CreateAsync(new CreateStaffUserInput("Report Rae", "rae@msp.test", [roles[RoleType.Technician]]));
        await svc.UpdateAsync(report.Id, new UpdateStaffUserInput("Report Rae", "rae@msp.test", null, null, manager.Id));

        var act = () => svc.DeleteAsync(manager.Id);

        await act.Should().NotThrowAsync();
        (await h.Db.AppUsers.SingleAsync(u => u.Id == report.Id)).ManagerId.Should().BeNull();
    }

    [Fact]
    public async Task You_cannot_delete_your_own_account()
    {
        var (h, _, roles) = await SetupAsync();
        await using var _ = h.Db;
        var me = new Desk.Domain.Identity.AppUser { MspOrganizationId = Org, DisplayName = "Admin", Email = "self3@msp.test", IsActive = true };
        h.Db.AppUsers.Add(me);
        await h.Db.SaveChangesAsync();

        var selfAsActor = new TestCurrentUser(Org, userId: me.Id);
        var svcAsSelf = new UserAdminService(h.Db, new AuditWriter(h.Db, selfAsActor, h.Tenant, h.Clock), h.Tenant, selfAsActor,
            new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock), new EffectivePermissionService(h.Db), h.Clock);

        var act = () => svcAsSelf.DeleteAsync(me.Id);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await h.Db.AppUsers.AnyAsync(u => u.Id == me.Id)).Should().BeTrue();
    }

    // ---- Departments ----

    [Fact]
    public async Task Setting_a_new_primary_department_unsets_the_previous_one()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane6@msp.test", [roles[RoleType.Technician]]));
        var deptA = new Department { MspOrganizationId = Org, Name = "IT Support" };
        var deptB = new Department { MspOrganizationId = Org, Name = "Billing" };
        h.Db.Departments.AddRange(deptA, deptB);
        await h.Db.SaveChangesAsync();

        await svc.SetDepartmentAsync(created.Id, deptA.Id, isPrimary: true);
        await svc.SetDepartmentAsync(created.Id, deptB.Id, isPrimary: true);

        var rows = await h.Db.UserDepartments.Where(d => d.AppUserId == created.Id).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Single(r => r.DepartmentId == deptB.Id).IsPrimary.Should().BeTrue();
        rows.Single(r => r.DepartmentId == deptA.Id).IsPrimary.Should().BeFalse();
    }

    // ---- Permission templates ----

    [Fact]
    public async Task Applying_a_template_materializes_its_entries_as_overrides_tagged_with_the_template()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane7@msp.test", [roles[RoleType.Technician]]));
        var template = new Desk.Domain.Authorization.PermissionTemplate { Name = "Custom", BaseRoleType = RoleType.Technician };
        template.Entries.Add(new Desk.Domain.Authorization.PermissionTemplateEntry
        {
            PermissionKey = Permissions.TicketsViewAll, Effect = PermissionEffect.Grant, Scope = PermissionScope.Department,
        });
        h.Db.PermissionTemplates.Add(template);
        await h.Db.SaveChangesAsync();

        await svc.ApplyPermissionTemplateAsync(created.Id, template.Id);

        var over = await h.Db.UserPermissionOverrides.SingleAsync(o => o.AppUserId == created.Id);
        over.PermissionKey.Should().Be(Permissions.TicketsViewAll);
        over.Scope.Should().Be(PermissionScope.Department);
        over.AppliedFromTemplateId.Should().Be(template.Id);
    }

    // ---- Effective permissions ----

    [Fact]
    public async Task Effective_permissions_covers_the_whole_catalogue()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        // SetupAsync's roles are bare (no RolePermission rows) — fine for the role-assignment tests
        // above, but this test specifically needs a real grant to resolve through.
        var technician = await h.Db.Roles.SingleAsync(r => r.Id == roles[RoleType.Technician]);
        h.Db.Set<Desk.Domain.Identity.RolePermission>().Add(new Desk.Domain.Identity.RolePermission
        {
            RoleId = technician.Id, PermissionKey = Permissions.TicketsViewAssigned, Scope = PermissionScope.Assigned,
        });
        await h.Db.SaveChangesAsync();
        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane8@msp.test", [roles[RoleType.Technician]]));

        var effective = await svc.GetEffectivePermissionsAsync(created.Id);

        effective.Should().HaveCount(Desk.Domain.Authorization.PermissionCatalog.Definitions.Count);
        effective.Should().ContainSingle(e => e.PermissionKey == Permissions.TicketsViewAssigned)
            .Which.Scope.Should().Be(PermissionScope.Assigned);
    }

    // ---- Bulk actions ----

    [Fact]
    public async Task Bulk_deactivate_skips_the_callers_own_row_without_failing_the_rest()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var me = new Desk.Domain.Identity.AppUser
        {
            MspOrganizationId = Org, DisplayName = "Admin", Email = "self4@msp.test",
            IdpSubject = h.User.Subject, IsActive = true,
        };
        h.Db.AppUsers.Add(me);
        await h.Db.SaveChangesAsync();
        var other = await svc.CreateAsync(new CreateStaffUserInput("Other", "other@msp.test", [roles[RoleType.Technician]]));

        var result = await svc.BulkAsync(new BulkUserActionInput(BulkUserAction.Deactivate, [me.Id, other.Id]));

        result.Rows.Single(r => r.UserId == me.Id).Success.Should().BeFalse();
        result.Rows.Single(r => r.UserId == other.Id).Success.Should().BeTrue();
        (await h.Db.AppUsers.SingleAsync(u => u.Id == other.Id)).IsActive.Should().BeFalse();
        // The self-guard on SetActiveAsync uses IdpSubject, not UserId — confirms it's still
        // reachable through the bulk path and doesn't silently bypass it.
        (await h.Db.AppUsers.SingleAsync(u => u.Id == me.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Bulk_role_assignment_applies_to_every_row()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var a = await svc.CreateAsync(new CreateStaffUserInput("User A", "a@msp.test", [roles[RoleType.Technician]]));
        var b = await svc.CreateAsync(new CreateStaffUserInput("User B", "b@msp.test", [roles[RoleType.Technician]]));

        var result = await svc.BulkAsync(new BulkUserActionInput(BulkUserAction.AssignRole, [a.Id, b.Id], RoleId: roles[RoleType.Auditor]));

        result.Rows.Should().OnlyContain(r => r.Success);
        (await h.Db.UserRoles.CountAsync(r => r.RoleId == roles[RoleType.Auditor])).Should().Be(2);
    }
}
