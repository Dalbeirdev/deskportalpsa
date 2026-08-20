using Desk.Application.Admin;
using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The Roles &amp; Permissions module (§6): custom tenant roles over read-only built-ins. The
/// security-relevant edges get non-vacuous coverage — built-in immutability, the edit-a-role-you-
/// hold guard, delete-while-held, and the AssignRoleAsync escalation hole this module's work
/// closed (assignment used to accept ANY role id, including PlatformSuperAdministrator).
/// </summary>
public class RoleAdminTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static async Task<(AdminHarness H, RoleAdminService Roles, UserAdminService Users, Dictionary<RoleType, Guid> Seeded)> SetupAsync()
    {
        var h = AdminHarness.Create(Org);
        var seeded = new Dictionary<RoleType, Guid>();
        foreach (var t in new[] { RoleType.MspAdministrator, RoleType.Technician, RoleType.ClientUser, RoleType.PlatformSuperAdministrator })
        {
            var role = new Desk.Domain.Identity.Role { Name = t.ToString(), BuiltInType = t, IsSystemRole = true };
            h.Db.Roles.Add(role);
            seeded[t] = role.Id;
        }
        await h.Db.SaveChangesAsync();
        var audit = new AuditWriter(h.Db, h.User, h.Tenant, h.Clock);
        var roles = new RoleAdminService(h.Db, audit, h.Tenant, h.User);
        var users = new UserAdminService(h.Db, audit, h.Tenant, h.User,
            new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock), new EffectivePermissionService(h.Db), h.Clock);
        return (h, roles, users, seeded);
    }

    private static SaveRoleInput SeniorTech(string name = "Senior Technician") => new(name,
    [
        new RoleGrantDto(Permissions.TicketsViewAll, PermissionScope.All),
        new RoleGrantDto(Permissions.TicketsUpdate, PermissionScope.Department),
    ]);

    [Fact]
    public async Task A_custom_role_is_created_listed_assignable_and_actually_grants_its_permissions()
    {
        var (h, roles, users, seeded) = await SetupAsync();
        await using var _ = h.Db;

        var created = await roles.CreateAsync(SeniorTech());

        // Listed alongside the built-ins, with its grants.
        var listed = (await roles.ListAsync()).Single(r => r.Id == created.Id);
        listed.IsSystemRole.Should().BeFalse();
        listed.Grants.Should().HaveCount(2);

        // Offered by the Users page's picker.
        (await users.StaffRolesAsync()).Should().Contain(r => r.Id == created.Id);

        // Assignable to a real user, and the grant RESOLVES through the effective-permission
        // engine — the whole point of a role, not just a row.
        var jane = await users.CreateAsync(new CreateStaffUserInput("Jane Tech", "jane@msp.test", [seeded[RoleType.Technician]]));
        await users.AssignRoleAsync(jane.Id, created.Id);
        var eff = await new EffectivePermissionService(h.Db).ResolveAsync(jane.Id, Permissions.TicketsViewAll);
        eff.Scope.Should().Be(PermissionScope.All);
    }

    [Fact]
    public async Task Built_in_roles_cannot_be_edited_or_deleted()
    {
        var (h, roles, _, seeded) = await SetupAsync();
        await using var _ = h.Db;

        var edit = () => roles.UpdateAsync(seeded[RoleType.Technician], SeniorTech());
        await edit.Should().ThrowAsync<ValidationFailedException>().WithMessage("*Built-in*");

        var delete = () => roles.DeleteAsync(seeded[RoleType.Technician]);
        await delete.Should().ThrowAsync<ValidationFailedException>().WithMessage("*Built-in*");
    }

    [Fact]
    public async Task You_cannot_edit_a_role_you_hold()
    {
        // The caller holds RolesManage legitimately — the guard is that widening a role you HOLD
        // widens your own access, so someone else must do it.
        var (h, _, _, _) = await SetupAsync();
        await using var _ = h.Db;
        var me = new Desk.Domain.Identity.AppUser
        { MspOrganizationId = Org, DisplayName = "Admin", Email = "me@msp.test", IsActive = true };
        h.Db.AppUsers.Add(me);
        await h.Db.SaveChangesAsync();

        var selfActor = new TestCurrentUser(Org, userId: me.Id);
        var svc = new RoleAdminService(h.Db, new AuditWriter(h.Db, selfActor, h.Tenant, h.Clock), h.Tenant, selfActor);
        var mine = await svc.CreateAsync(SeniorTech());
        h.Db.UserRoles.Add(new Desk.Domain.Identity.UserRole { AppUserId = me.Id, RoleId = mine.Id });
        await h.Db.SaveChangesAsync();

        var act = () => svc.UpdateAsync(mine.Id, SeniorTech("Widened"));

        await act.Should().ThrowAsync<ForbiddenException>();
        (await h.Db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Id == mine.Id))
            .Name.Should().Be("Senior Technician", "the refused edit must not have partially applied");
    }

    [Fact]
    public async Task A_role_still_held_by_users_cannot_be_deleted()
    {
        var (h, roles, users, seeded) = await SetupAsync();
        await using var _ = h.Db;
        var custom = await roles.CreateAsync(SeniorTech());
        var jane = await users.CreateAsync(new CreateStaffUserInput("Jane", "jane@msp.test", [seeded[RoleType.Technician]]));
        await users.AssignRoleAsync(jane.Id, custom.Id);

        var act = () => roles.DeleteAsync(custom.Id);
        await act.Should().ThrowAsync<ValidationFailedException>().WithMessage("*still hold*");

        // Removing the assignment unblocks the delete — proving the guard tracked the holder, not
        // some unrelated condition.
        await users.RemoveRoleAsync(jane.Id, custom.Id);
        await roles.DeleteAsync(custom.Id);
        (await h.Db.Roles.AnyAsync(r => r.Id == custom.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task An_unsupported_scope_and_an_unknown_key_are_both_refused()
    {
        var (h, roles, _, _) = await SetupAsync();
        await using var _ = h.Db;

        // tickets.view.all is declared with exactly one legal scope (All) — Department is a scope
        // the engine would silently ignore, so the catalogue-driven validation must refuse it.
        var badScope = () => roles.CreateAsync(new SaveRoleInput("Bad Scope",
            [new RoleGrantDto(Permissions.TicketsViewAll, PermissionScope.Department)]));
        await badScope.Should().ThrowAsync<ValidationFailedException>().WithMessage("*scope*");

        var badKey = () => roles.CreateAsync(new SaveRoleInput("Bad Key",
            [new RoleGrantDto("tickets.made.up", PermissionScope.All)]));
        await badKey.Should().ThrowAsync<ValidationFailedException>().WithMessage("*not a known permission*");
    }

    [Fact]
    public async Task Assigning_a_client_or_platform_role_to_a_colleague_is_refused()
    {
        // The escalation hole this module closed: AssignRoleAsync used to accept ANY role id, so
        // two admins could hand each other PlatformSuperAdministrator. The target here is a
        // COLLEAGUE, not the caller — the pre-existing self-guard never covered this path.
        var (h, _, users, seeded) = await SetupAsync();
        await using var _ = h.Db;
        var jane = await users.CreateAsync(new CreateStaffUserInput("Jane", "jane@msp.test", [seeded[RoleType.Technician]]));

        foreach (var forbidden in new[] { seeded[RoleType.ClientUser], seeded[RoleType.PlatformSuperAdministrator] })
        {
            var act = () => users.AssignRoleAsync(jane.Id, forbidden);
            await act.Should().ThrowAsync<ValidationFailedException>();
        }
        (await h.Db.UserRoles.CountAsync(r => r.AppUserId == jane.Id)).Should().Be(1, "only the Technician role from creation");
    }

    [Fact]
    public async Task A_duplicate_role_name_is_refused_case_insensitively_even_against_built_ins()
    {
        var (h, roles, _, _) = await SetupAsync();
        await using var _ = h.Db;

        var act = () => roles.CreateAsync(new SaveRoleInput("TECHNICIAN",
            [new RoleGrantDto(Permissions.TicketsViewAll, PermissionScope.All)]));

        await act.Should().ThrowAsync<ValidationFailedException>().WithMessage("*already exists*");
    }
}
