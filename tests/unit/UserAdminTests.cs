using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Infrastructure.Admin;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Adding the MSP's own technicians. The account is created here; sign-in arrives on their first
/// IdP login, bound by verified email — so the dangerous edges are role hygiene (staff roles only)
/// and lockout (you cannot deactivate yourself).
/// </summary>
public class UserAdminTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static async Task<(AdminHarness H, UserAdminService Svc, Dictionary<RoleType, Guid> Roles)> SetupAsync()
    {
        var h = AdminHarness.Create(Org);
        var roles = new Dictionary<RoleType, Guid>();
        foreach (var t in new[] { RoleType.MspAdministrator, RoleType.Technician, RoleType.ClientUser, RoleType.PlatformSuperAdministrator })
        {
            var role = new Desk.Domain.Identity.Role { Name = t.ToString(), BuiltInType = t, IsSystemRole = true };
            h.Db.Roles.Add(role);
            roles[t] = role.Id;
        }
        await h.Db.SaveChangesAsync();
        var svc = new UserAdminService(h.Db, new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), h.Tenant, h.User);
        return (h, svc, roles);
    }

    [Fact]
    public async Task A_technician_is_created_unlinked_and_appears_in_the_list()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;

        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane Tech", "jane@msp.test", [roles[RoleType.Technician]]));

        created.SignInLinked.Should().BeFalse(); // an invitation until their first IdP login
        created.Roles.Should().ContainSingle().Which.Name.Should().Be("Technician");
        var listed = await svc.ListAsync();
        listed.Should().ContainSingle(u => u.Email == "jane@msp.test" && u.IsActive);
    }

    [Fact]
    public async Task A_duplicate_email_is_refused_because_it_is_the_sign_in_binding_key()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane@msp.test", [roles[RoleType.Technician]]));

        var act = () => svc.CreateAsync(new CreateStaffUserInput("Other Jane", "JANE@msp.test", [roles[RoleType.Technician]]));

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Client_and_platform_roles_cannot_be_handed_to_staff()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;

        foreach (var forbidden in new[] { roles[RoleType.ClientUser], roles[RoleType.PlatformSuperAdministrator] })
        {
            var act = () => svc.CreateAsync(new CreateStaffUserInput("X", $"x{forbidden:N}@msp.test", [forbidden]));
            await act.Should().ThrowAsync<ValidationFailedException>();
        }
        // and the catalogue offered to the page never contains them in the first place
        (await svc.StaffRolesAsync()).Select(r => r.Name)
            .Should().BeEquivalentTo("MspAdministrator", "Technician");
    }

    [Fact]
    public async Task You_cannot_deactivate_your_own_account()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        // The caller's own row, bound to the subject the harness authenticates as.
        var me = new Desk.Domain.Identity.AppUser
        {
            MspOrganizationId = Org, DisplayName = "Admin", Email = "admin@msp.test",
            IdpSubject = h.User.Subject, IsActive = true,
        };
        h.Db.AppUsers.Add(me);
        await h.Db.SaveChangesAsync();

        var act = () => svc.SetActiveAsync(me.Id, active: false);

        await act.Should().ThrowAsync<ValidationFailedException>();
        (await h.Db.AppUsers.SingleAsync(u => u.Id == me.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivating_someone_else_works_and_reactivation_restores_them()
    {
        var (h, svc, roles) = await SetupAsync();
        await using var _ = h.Db;
        var created = await svc.CreateAsync(new CreateStaffUserInput("Jane", "jane@msp.test", [roles[RoleType.Technician]]));

        await svc.SetActiveAsync(created.Id, active: false);
        (await svc.ListAsync()).Single(u => u.Id == created.Id).IsActive.Should().BeFalse();

        await svc.SetActiveAsync(created.Id, active: true);
        (await svc.ListAsync()).Single(u => u.Id == created.Id).IsActive.Should().BeTrue();
    }
}
