using Desk.Application.Admin;
using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Authorization;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Departments & Teams admin CRUD (§9/§10) — the org-structure rows Users & Access Management
/// assigns people to. Deliberately exercises the two-list split (DepartmentsAsync's active-only
/// picker vs. DepartmentsManageAsync's everything-including-inactive admin view) and the
/// cascade-on-delete behavior non-vacuously, since both are easy to get silently wrong.
/// </summary>
public class DepartmentTeamAdminTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static (AdminHarness H, UserAdminService Svc) Setup()
    {
        var h = AdminHarness.Create(Org);
        var svc = new UserAdminService(h.Db, new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), h.Tenant, h.User,
            new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock), new EffectivePermissionService(h.Db), h.Clock);
        return (h, svc);
    }

    [Fact]
    public async Task Creating_a_department_makes_it_available_with_zero_teams_and_zero_users()
    {
        var (h, svc) = Setup();
        await using var _ = h.Db;

        var created = await svc.CreateDepartmentAsync(new CreateDepartmentInput("Field Services", "On-site work"));

        created.Name.Should().Be("Field Services");
        created.Teams.Should().BeEmpty();
        created.PrimaryUserCount.Should().Be(0);
        (await svc.DepartmentsManageAsync()).Should().ContainSingle(d => d.Id == created.Id);
    }

    [Fact]
    public async Task A_duplicate_department_name_is_refused_case_insensitively()
    {
        var (h, svc) = Setup();
        await using var _ = h.Db;
        await svc.CreateDepartmentAsync(new CreateDepartmentInput("Field Services", null));

        var act = () => svc.CreateDepartmentAsync(new CreateDepartmentInput("FIELD SERVICES", null));

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Updating_a_department_changes_its_name_and_description()
    {
        var (h, svc) = Setup();
        await using var _ = h.Db;
        var created = await svc.CreateDepartmentAsync(new CreateDepartmentInput("Field Services", null));

        var updated = await svc.UpdateDepartmentAsync(created.Id, new UpdateDepartmentInput("Onsite Services", "Renamed"));

        updated.Name.Should().Be("Onsite Services");
        updated.Description.Should().Be("Renamed");
    }

    [Fact]
    public async Task Deactivating_a_department_hides_it_from_the_picker_but_not_the_admin_view()
    {
        // Non-vacuous: proves BOTH halves — still visible where it should be, gone where it
        // shouldn't — rather than only checking the list it's supposed to disappear from.
        var (h, svc) = Setup();
        await using var _ = h.Db;
        var created = await svc.CreateDepartmentAsync(new CreateDepartmentInput("Field Services", null));

        await svc.SetDepartmentActiveAsync(created.Id, active: false);

        (await svc.DepartmentsAsync()).Should().NotContain(d => d.Id == created.Id);
        (await svc.DepartmentsManageAsync()).Should().ContainSingle(d => d.Id == created.Id && !d.IsActive);
    }

    [Fact]
    public async Task Deleting_a_department_cascades_to_its_teams_and_every_users_membership()
    {
        var (h, svc) = Setup();
        await using var _ = h.Db;
        var dept = await svc.CreateDepartmentAsync(new CreateDepartmentInput("Field Services", null));
        var team = await svc.CreateTeamAsync(new CreateTeamInput(dept.Id, "Level 1"));
        var user = await svc.CreateAsync(new CreateStaffUserInput("Jane Tech", "jane@msp.test",
            [(await SeedTechnicianRole(h)).Id]));
        await svc.SetDepartmentAsync(user.Id, dept.Id, isPrimary: true);
        await svc.AssignTeamAsync(user.Id, team.Id);

        await svc.DeleteDepartmentAsync(dept.Id);

        (await svc.DepartmentsManageAsync()).Should().NotContain(d => d.Id == dept.Id);
        var refreshed = await svc.GetAsync(user.Id);
        refreshed!.PrimaryDepartment.Should().BeNull();
        refreshed.Teams.Should().BeEmpty();
    }

    [Fact]
    public async Task Team_names_are_unique_within_a_department_but_not_across_departments()
    {
        var (h, svc) = Setup();
        await using var _ = h.Db;
        var deptA = await svc.CreateDepartmentAsync(new CreateDepartmentInput("Dept A", null));
        var deptB = await svc.CreateDepartmentAsync(new CreateDepartmentInput("Dept B", null));
        await svc.CreateTeamAsync(new CreateTeamInput(deptA.Id, "Level 1"));

        var sameDept = () => svc.CreateTeamAsync(new CreateTeamInput(deptA.Id, "Level 1"));
        await sameDept.Should().ThrowAsync<ValidationFailedException>();

        var otherDept = await svc.CreateTeamAsync(new CreateTeamInput(deptB.Id, "Level 1"));
        otherDept.Name.Should().Be("Level 1");
    }

    [Fact]
    public async Task Deleting_a_team_removes_it_without_touching_its_departments_other_teams()
    {
        var (h, svc) = Setup();
        await using var _ = h.Db;
        var dept = await svc.CreateDepartmentAsync(new CreateDepartmentInput("Field Services", null));
        var keep = await svc.CreateTeamAsync(new CreateTeamInput(dept.Id, "Level 1"));
        var drop = await svc.CreateTeamAsync(new CreateTeamInput(dept.Id, "Level 2"));

        await svc.DeleteTeamAsync(drop.Id);

        var manage = (await svc.DepartmentsManageAsync()).Single(d => d.Id == dept.Id);
        manage.Teams.Should().ContainSingle(t => t.Id == keep.Id);
    }

    private static async Task<RoleOptionDto> SeedTechnicianRole(AdminHarness h)
    {
        var role = new Desk.Domain.Identity.Role { Name = "Technician", BuiltInType = RoleType.Technician, IsSystemRole = true };
        h.Db.Roles.Add(role);
        await h.Db.SaveChangesAsync();
        return new RoleOptionDto(role.Id, role.Name);
    }
}
