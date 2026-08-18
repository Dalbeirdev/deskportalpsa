using Desk.Domain.Audit;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

public class AuditAndRbacTests
{
    private static readonly Guid Org = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Audit_entries_are_append_only()
    {
        var db = Guid.NewGuid().ToString();
        Guid entryId;

        await using (var write = TestDbContextFactory.ForTenant(db, Org))
        {
            var entry = new AuditLogEntry
            {
                MspOrganizationId = Org,
                Action = "connection.created",
                EntityType = "PsaConnection",
            };
            write.AuditLog.Add(entry);
            await write.SaveChangesAsync();
            entryId = entry.Id;
        }

        await using var edit = TestDbContextFactory.ForPlatform(db);
        var existing = await edit.AuditLog.SingleAsync(a => a.Id == entryId);
        existing.Action = "tampered";

        var act = async () => await edit.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public async Task Seeder_creates_seven_built_in_roles()
    {
        var db = Guid.NewGuid().ToString();
        await using var ctx = TestDbContextFactory.ForPlatform(db);

        await DatabaseSeeder.SeedBuiltInRolesAsync(ctx);
        await DatabaseSeeder.SeedBuiltInRolesAsync(ctx); // idempotent — second run adds nothing

        var roles = await ctx.Roles.IgnoreQueryFilters().Include(r => r.Permissions).ToListAsync();
        roles.Should().HaveCount(7);
        roles.Should().OnlyContain(r => r.IsSystemRole);
    }

    [Fact]
    public void Super_admin_role_has_every_permission()
    {
        Permissions.ForRole(RoleType.PlatformSuperAdministrator).Select(p => p.Key)
            .Should().BeEquivalentTo(Permissions.All);
    }

    [Theory]
    [InlineData(RoleType.ClientUser, Permissions.TicketsCreate, true)]
    [InlineData(RoleType.ClientUser, Permissions.ConnectionsManage, false)]
    [InlineData(RoleType.Technician, Permissions.TicketsViewAssigned, true)]
    [InlineData(RoleType.Technician, Permissions.TicketsViewAll, false)]
    [InlineData(RoleType.Auditor, Permissions.AuditView, true)]
    [InlineData(RoleType.Auditor, Permissions.TicketsCreate, false)]
    public void Role_permission_sets_match_least_privilege(RoleType role, string permission, bool expected)
    {
        Permissions.ForRole(role).Any(p => p.Key == permission).Should().Be(expected);
    }
}
