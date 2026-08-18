using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Organization;
using Desk.Infrastructure.Authorization;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

public class EffectivePermissionServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static DeskDbContext NewDb(string? dbName = null)
    {
        var tenant = new TenantContext();
        tenant.SetPlatformScope();
        var options = new DbContextOptionsBuilder<DeskDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new DeskDbContext(options, tenant, new TestClock());
    }

    private static async Task<(Guid UserId, Guid RoleId)> SeedUserWithRoleAsync(
        DeskDbContext db, params (string Key, PermissionScope Scope)[] permissions)
    {
        var role = new Role { Name = "Test Role " + Guid.NewGuid(), MspOrganizationId = Org };
        foreach (var (key, scope) in permissions)
            role.Permissions.Add(new RolePermission { PermissionKey = key, Scope = scope });
        db.Roles.Add(role);

        var user = new Desk.Domain.Identity.AppUser
        {
            MspOrganizationId = Org, Email = $"{Guid.NewGuid()}@test", DisplayName = "T",
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return (user.Id, role.Id);
    }

    [Fact]
    public async Task No_role_grant_resolves_to_none()
    {
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db);

        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.Scope.Should().Be(PermissionScope.None);
        eff.Source.Should().Be(Desk.Application.Authorization.PermissionSource.NoGrant);
    }

    [Fact]
    public async Task A_role_grant_resolves_to_its_own_scope()
    {
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db, (Permissions.TicketsUpdate, PermissionScope.Assigned));

        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.Scope.Should().Be(PermissionScope.Assigned);
        eff.Source.Should().Be(Desk.Application.Authorization.PermissionSource.RoleGrant);
    }

    [Fact]
    public async Task Multiple_roles_union_to_the_most_permissive_scope()
    {
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db, (Permissions.TicketsUpdate, PermissionScope.Assigned));
        var extraRole = new Role { Name = "Extra", MspOrganizationId = Org };
        extraRole.Permissions.Add(new RolePermission { PermissionKey = Permissions.TicketsUpdate, Scope = PermissionScope.Department });
        db.Roles.Add(extraRole);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { AppUserId = userId, RoleId = extraRole.Id });
        await db.SaveChangesAsync();

        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.Scope.Should().Be(PermissionScope.Department, "Department outranks Assigned, and roles union to the widest grant");
    }

    [Fact]
    public async Task A_deny_override_beats_a_role_grant_of_All()
    {
        // The case the whole override mechanism exists for: a role can grant everything and a
        // single per-user row still shuts it off, with no need to touch the role at all.
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db, (Permissions.TicketsUpdate, PermissionScope.All));
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            MspOrganizationId = Org, AppUserId = userId, PermissionKey = Permissions.TicketsUpdate,
            Effect = PermissionEffect.Deny,
        });
        await db.SaveChangesAsync();

        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.IsDenied.Should().BeTrue();
        eff.Source.Should().Be(Desk.Application.Authorization.PermissionSource.OverrideDeny);
    }

    [Fact]
    public async Task A_grant_override_hands_out_a_permission_no_role_held()
    {
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db); // no roles grant anything
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            MspOrganizationId = Org, AppUserId = userId, PermissionKey = Permissions.TicketsUpdate,
            Effect = PermissionEffect.Grant, Scope = PermissionScope.Own,
        });
        await db.SaveChangesAsync();

        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.Scope.Should().Be(PermissionScope.Own);
        eff.Source.Should().Be(Desk.Application.Authorization.PermissionSource.OverrideGrant);
    }

    [Fact]
    public async Task An_override_replaces_the_role_scope_rather_than_widening_it()
    {
        // Explicitly pinning the REPLACE semantics: a role grant of All plus an override of Own
        // must land on Own, not "All because that's wider" — the override is the complete answer.
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db, (Permissions.TicketsUpdate, PermissionScope.All));
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            MspOrganizationId = Org, AppUserId = userId, PermissionKey = Permissions.TicketsUpdate,
            Effect = PermissionEffect.Grant, Scope = PermissionScope.Own,
        });
        await db.SaveChangesAsync();

        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.Scope.Should().Be(PermissionScope.Own);
    }

    [Fact]
    public async Task Absent_board_access_row_defaults_to_all_boards()
    {
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db, (Permissions.TicketsUpdate, PermissionScope.All));

        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.BoardMode.Should().Be(BoardAccessMode.All);
    }

    [Fact]
    public async Task Selected_board_access_returns_only_grants_covering_the_required_action()
    {
        var db = NewDb();
        var (userId, _) = await SeedUserWithRoleAsync(db, (Permissions.TicketsUpdate, PermissionScope.All));
        var connId = Guid.NewGuid();
        db.UserBoardAccesses.Add(new UserBoardAccess { MspOrganizationId = Org, AppUserId = userId, Mode = BoardAccessMode.Selected });
        db.UserBoardGrants.Add(new UserBoardGrant
        {
            MspOrganizationId = Org, AppUserId = userId, PsaConnectionId = connId,
            BoardName = "Help Desk", Actions = BoardAction.View, // View only — does NOT cover Edit
        });
        await db.SaveChangesAsync();

        // TicketsUpdate requires Edit (see PermissionCatalog) — the View-only grant must not count.
        var eff = await new EffectivePermissionService(db).ResolveAsync(userId, Permissions.TicketsUpdate);

        eff.BoardMode.Should().Be(BoardAccessMode.Selected);
        eff.BoardGrants.Should().BeEmpty();
    }
}
