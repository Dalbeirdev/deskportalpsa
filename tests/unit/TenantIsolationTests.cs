using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Phase-2 headline acceptance criterion: cross-tenant data must be unreachable at the data
/// layer. These tests exercise the DeskDbContext global query filter and write guards directly.
/// </summary>
public class TenantIsolationTests
{
    private static readonly Guid OrgA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrgB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static PsaConnection NewConnection(Guid org, string name) => new()
    {
        MspOrganizationId = org,
        Name = name,
        Provider = ProviderType.ConnectWisePsa,
        ApiEndpoint = "https://api.example.com",
        CredentialSecretRef = "mem://ignored",
    };

    [Fact]
    public async Task Query_filter_hides_other_tenants_rows()
    {
        var db = Guid.NewGuid().ToString();

        // Seed one connection for each org (platform scope bypasses the filter for seeding).
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.PsaConnections.Add(NewConnection(OrgA, "A-conn"));
            seed.PsaConnections.Add(NewConnection(OrgB, "B-conn"));
            await seed.SaveChangesAsync();
        }

        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);
        var visible = await asA.PsaConnections.ToListAsync();

        visible.Should().ContainSingle();
        visible[0].Name.Should().Be("A-conn");
        visible.Should().OnlyContain(c => c.MspOrganizationId == OrgA);
    }

    [Fact]
    public async Task Direct_lookup_of_other_tenants_row_returns_null()
    {
        var db = Guid.NewGuid().ToString();
        var bId = Guid.NewGuid();

        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            var b = NewConnection(OrgB, "B-conn");
            b.Id = bId;
            seed.PsaConnections.Add(b);
            await seed.SaveChangesAsync();
        }

        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);
        // Even with the exact primary key, org A cannot read org B's row.
        var found = await asA.PsaConnections.FirstOrDefaultAsync(c => c.Id == bId);
        found.Should().BeNull();
    }

    [Fact]
    public async Task Write_is_stamped_with_current_tenant()
    {
        var db = Guid.NewGuid().ToString();
        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);

        // Note: MspOrganizationId intentionally left default; the context must stamp it.
        var conn = new PsaConnection
        {
            Name = "auto-stamped",
            Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://api.example.com",
            CredentialSecretRef = "mem://x",
        };
        asA.PsaConnections.Add(conn);
        await asA.SaveChangesAsync();

        conn.MspOrganizationId.Should().Be(OrgA);
    }

    [Fact]
    public async Task Cross_tenant_insert_is_blocked()
    {
        var db = Guid.NewGuid().ToString();
        await using var asA = TestDbContextFactory.ForTenant(db, OrgA);

        // Attempt to insert a row belonging to org B while scoped to org A.
        asA.PsaConnections.Add(NewConnection(OrgB, "smuggled"));

        var act = async () => await asA.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cross-tenant write blocked*");
    }

    [Fact]
    public async Task Unscoped_context_sees_nothing_fail_closed()
    {
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.PsaConnections.Add(NewConnection(OrgA, "A-conn"));
            await seed.SaveChangesAsync();
        }

        await using var unscoped = TestDbContextFactory.Unscoped(db);
        var visible = await unscoped.PsaConnections.ToListAsync();
        visible.Should().BeEmpty();
    }

    [Fact]
    public async Task Platform_scope_sees_all_tenants()
    {
        var db = Guid.NewGuid().ToString();
        await using (var seed = TestDbContextFactory.ForPlatform(db))
        {
            seed.PsaConnections.Add(NewConnection(OrgA, "A-conn"));
            seed.PsaConnections.Add(NewConnection(OrgB, "B-conn"));
            await seed.SaveChangesAsync();
        }

        await using var platform = TestDbContextFactory.ForPlatform(db);
        (await platform.PsaConnections.CountAsync()).Should().Be(2);
    }
}
