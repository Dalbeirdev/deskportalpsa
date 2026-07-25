using Desk.Application.Abstractions;
using Desk.Application.Analytics;
using Desk.Application.Tickets;
using Desk.Domain.Audit;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Analytics;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tickets;
using Desk.Infrastructure.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Adversarial cross-tenant tests over the phase 6-8 services. Both tenants' data lives in ONE
/// shared in-memory store (via a shared InMemoryDatabaseRoot) so a reader that fails to scope would
/// actually see the other tenant — the isolation is proven, not passed vacuously. Special attention
/// to AuditLog and AppUser, which the DbContext global filter does NOT cover.
/// </summary>
public class SecurityIsolationTests
{
    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();

    /// <summary>A single shared in-memory store two tenant-scoped contexts both read from.</summary>
    private sealed class Store
    {
        private readonly InMemoryDatabaseRoot _root = new();
        private readonly string _name = Guid.NewGuid().ToString();

        private DeskDbContext Build(ITenantContext tenant) =>
            new(new DbContextOptionsBuilder<DeskDbContext>().UseInMemoryDatabase(_name, _root).Options, tenant, new TestClock());

        public DeskDbContext Platform()
        {
            var t = new TenantContext(); t.SetPlatformScope(); return Build(t);
        }

        public (DeskDbContext db, TenantContext tenant) Tenant(Guid org)
        {
            var t = new TenantContext(); t.SetTenant(org); return (Build(t), t);
        }
    }

    [Fact]
    public async Task Audit_query_never_returns_another_tenants_entries()
    {
        var store = new Store();
        await using (var seed = store.Platform())
        {
            seed.AuditLog.Add(new AuditLogEntry { MspOrganizationId = OrgA, Action = "a.action", EntityType = "X" });
            seed.AuditLog.Add(new AuditLogEntry { MspOrganizationId = OrgB, Action = "b.secret", EntityType = "X" });
            await seed.SaveChangesAsync();
        }

        var (db, tenant) = store.Tenant(OrgA);
        var entries = await new AuditQueryService(db, tenant).ListAsync();

        entries.Should().ContainSingle().Which.Action.Should().Be("a.action");
        entries.Should().NotContain(e => e.Action == "b.secret");
    }

    [Fact]
    public async Task User_admin_list_never_returns_another_tenants_users()
    {
        var store = new Store();
        await using (var seed = store.Platform())
        {
            seed.AppUsers.Add(new AppUser { MspOrganizationId = OrgA, Email = "a@a", DisplayName = "A User", IdpSubject = "sa" });
            seed.AppUsers.Add(new AppUser { MspOrganizationId = OrgB, Email = "b@b", DisplayName = "B User", IdpSubject = "sb" });
            await seed.SaveChangesAsync();
        }

        var (db, tenant) = store.Tenant(OrgA);
        var audit = new AuditWriter(db, new TestCurrentUser(OrgA), tenant, new TestClock());
        var users = await new UserAdminService(db, audit, tenant).ListAsync();

        users.Should().ContainSingle().Which.DisplayName.Should().Be("A User");
    }

    [Fact]
    public async Task Client_portal_read_cannot_reach_another_tenants_ticket()
    {
        var store = new Store();
        var connB = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        await using (var seed = store.Platform())
        {
            seed.PsaConnections.Add(new PsaConnection { Id = connB, MspOrganizationId = OrgB, Name = "B", Provider = ProviderType.AutotaskPsa, ApiEndpoint = "x", CredentialSecretRef = "m" });
            seed.ClientCompanies.Add(new ClientCompany { Id = companyB, MspOrganizationId = OrgB, PsaConnectionId = connB, Name = "B Co", ExternalCompanyId = "1" });
            seed.Tickets.Add(new Ticket { MspOrganizationId = OrgB, PsaConnectionId = connB, Provider = ProviderType.AutotaskPsa, ClientCompanyId = companyB, RequesterName = "r", RequesterEmail = "r@x", Title = "B-secret", PortalStatus = "NEW", PortalPriority = "NORMAL" });
            await seed.SaveChangesAsync();
        }

        var (db, _) = store.Tenant(OrgA);
        // Even naming org B's exact company id, the tenant filter hides the row entirely.
        var access = new ClientAccess(OrgA, companyB, Guid.NewGuid(), true);
        (await new TicketReadService(db).ListAsync(access)).Should().BeEmpty();
    }

    [Fact]
    public async Task Dashboard_metrics_never_count_another_tenants_tickets()
    {
        var store = new Store();
        await using (var seed = store.Platform())
        {
            seed.Tickets.Add(new Ticket { MspOrganizationId = OrgB, PsaConnectionId = Guid.NewGuid(), Provider = ProviderType.AutotaskPsa, ClientCompanyId = Guid.NewGuid(), RequesterName = "r", RequesterEmail = "r@x", Title = "B", PortalStatus = "NEW", PortalPriority = "NORMAL", AssignedTechnicianExternalId = "R1" });
            await seed.SaveChangesAsync();
        }

        var (db, _) = store.Tenant(OrgA);
        var svc = new TechnicianMetricsService(db, new ProductivityScorer(), new TestClock());
        var m = await svc.ForTechnicianAsync(new MetricsFilter { TechnicianExternalId = "R1" }, ProductivityWeights.Default);
        m.Assigned.Should().Be(0); // org B's ticket is invisible to org A
    }
}
