using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Application.Tickets;
using Desk.Connectors.Mock;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Sync;
using Desk.Infrastructure.Tickets;
using Desk.PsaCore.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

public class ClientPortalTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid AdminUser = Guid.NewGuid();
    private static readonly Guid RegularUser = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    private static async Task<DeskDbContext> SeedAsync(string dbName)
    {
        var db = TestDbContextFactory.ForPlatform(dbName);
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "CW", Provider = ProviderType.ConnectWisePsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        db.ClientCompanies.Add(new ClientCompany { Id = CompanyA, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "A", ExternalCompanyId = "1" });
        db.ClientCompanies.Add(new ClientCompany { Id = CompanyB, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "B", ExternalCompanyId = "2" });
        db.ClientUsers.Add(new ClientUser { Id = AdminUser, MspOrganizationId = Org, ClientCompanyId = CompanyA, Email = "admin@a.test", DisplayName = "Admin A", IdpSubject = "sub-admin", IsCompanyAdministrator = true });
        db.ClientUsers.Add(new ClientUser { Id = RegularUser, MspOrganizationId = Org, ClientCompanyId = CompanyA, Email = "user@a.test", DisplayName = "User A", IdpSubject = "sub-user" });
        await db.SaveChangesAsync();
        return db;
    }

    private static Ticket Ticket(Guid company, Guid requester, string title) => new()
    {
        MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.ConnectWisePsa,
        ExternalTicketId = title, ClientCompanyId = company, RequesterUserId = requester,
        RequesterName = "R", RequesterEmail = "r@test", Title = title, PortalStatus = "NEW", PortalPriority = "NORMAL",
    };

    private static ClientAccess Access(Guid company, Guid user, bool admin) => new(Org, company, user, admin);

    [Fact]
    public async Task Company_admin_sees_all_company_tickets_others_see_only_their_own()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = await SeedAsync(dbName);
        db.Tickets.Add(Ticket(CompanyA, RegularUser, "own"));
        db.Tickets.Add(Ticket(CompanyA, AdminUser, "other"));
        await db.SaveChangesAsync();
        var reads = new TicketReadService(db, new NoopTicketScopeQuery(), new TestCurrentUser(Org));

        (await reads.ListAsync(Access(CompanyA, AdminUser, true))).Should().HaveCount(2);
        var mine = await reads.ListAsync(Access(CompanyA, RegularUser, false));
        mine.Should().ContainSingle().Which.Title.Should().Be("own");
    }

    [Fact]
    public async Task Client_cannot_see_another_companys_ticket()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = await SeedAsync(dbName);
        var bTicket = Ticket(CompanyB, Guid.NewGuid(), "b-secret");
        db.Tickets.Add(bTicket);
        await db.SaveChangesAsync();
        var reads = new TicketReadService(db, new NoopTicketScopeQuery(), new TestCurrentUser(Org));

        (await reads.ListAsync(Access(CompanyA, AdminUser, true))).Should().BeEmpty();
        (await reads.GetDetailAsync(Access(CompanyA, AdminUser, true), bTicket.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Detail_never_returns_internal_notes()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = await SeedAsync(dbName);
        var t = Ticket(CompanyA, RegularUser, "t");
        db.Tickets.Add(t);
        db.TicketNotes.Add(new TicketNote { MspOrganizationId = Org, TicketId = t.Id, AuthorName = "Tech", Body = "public reply", IsPublic = true });
        db.TicketNotes.Add(new TicketNote { MspOrganizationId = Org, TicketId = t.Id, AuthorName = "Tech", Body = "INTERNAL secret", IsPublic = false });
        await db.SaveChangesAsync();

        var detail = await new TicketReadService(db, new NoopTicketScopeQuery(), new TestCurrentUser(Org)).GetDetailAsync(Access(CompanyA, RegularUser, false), t.Id);

        detail!.Conversation.Should().ContainSingle().Which.Body.Should().Be("public reply");
        detail.Conversation.Should().NotContain(n => n.Body.Contains("INTERNAL"));
    }

    [Fact]
    public async Task Create_writes_to_psa_then_persists_and_records_portal_event()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedAsync(dbName);
        var events = new SyncEventStore(db, clock);
        var svc = new TicketCommandService(db, new FakeResolver(new MockConnector(new MockConnectorOptions(), clock)),
            new MappingEngine(), events, new NoopTicketScopeQuery(), clock, new RecordingActivity());

        var result = await svc.CreateAsync(Access(CompanyA, RegularUser, false),
            new CreateTicketInput("Printer down", "It smokes", "HIGH", null, "Service Desk"));

        var ticket = await db.Tickets.SingleAsync();
        ticket.ExternalTicketId.Should().NotBeNullOrEmpty(); // came back from the PSA
        ticket.RequesterUserId.Should().Be(RegularUser);
        result.ExternalTicketId.Should().Be(ticket.ExternalTicketId);

        // A portal-origin sync event was recorded for echo suppression.
        var evt = await db.SyncEvents.SingleAsync();
        evt.SourceMarker.Should().Be(SyncSource.Portal);
        evt.PayloadHash.Should().Be(ticket.UpdateHash);
    }

    [Fact]
    public async Task A_staff_internal_note_is_stored_internal_and_pushed_flagged_internal()
    {
        // The composer's "Internal note" toggle. Two things must BOTH be true or a private remark
        // leaks: the stored row carries IsPublic=false (the client read path filters on it), and
        // the provider push carries IsPublic=false (so the PSA flags it internal on its side too).
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedAsync(dbName);
        var mock = new MockConnector(new MockConnectorOptions(), clock);
        var svc = new TicketCommandService(db, new FakeResolver(mock),
            new MappingEngine(), new SyncEventStore(db, clock), new NoopTicketScopeQuery(), clock, new RecordingActivity());

        // Create through the service so the ticket exists in the mock PSA before commenting.
        var created = await svc.CreateAsync(Access(CompanyA, RegularUser, false),
            new CreateTicketInput("New issue", null, null, null, null));

        await svc.AddStaffCommentAsync(Guid.NewGuid(), "Jane Tech", created.Id, "Internal analysis.", isPublic: false);

        var stored = await db.TicketNotes.SingleAsync(n => n.Body == "Internal analysis.");
        stored.IsPublic.Should().BeFalse();
        var pushed = (await mock.GetNotesAsync(created.ExternalTicketId!)).Single(n => n.Body == "Internal analysis.");
        pushed.IsPublic.Should().BeFalse("the PSA must receive the internal flag, not just our copy");
    }

    [Fact]
    public async Task Comment_posts_public_note_and_records_portal_event()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedAsync(dbName);
        var t = Ticket(CompanyA, RegularUser, "T-1");
        db.Tickets.Add(t);
        await db.SaveChangesAsync();

        var svc = new TicketCommandService(db, new FakeResolver(new MockConnector(new MockConnectorOptions(), clock)),
            new MappingEngine(), new SyncEventStore(db, clock), new NoopTicketScopeQuery(), clock, new RecordingActivity());

        // Create through the service so the ticket exists in the (shared) mock PSA before commenting.
        var created = await svc.CreateAsync(Access(CompanyA, RegularUser, false),
            new CreateTicketInput("New issue", null, null, null, null));

        var note = await svc.AddCommentAsync(Access(CompanyA, RegularUser, false), created.Id, "Any update?");

        note.AuthoredByClient.Should().BeTrue();
        (await db.TicketNotes.CountAsync(n => n.IsPublic)).Should().Be(1);
        (await db.SyncEvents.CountAsync(e => e.SourceMarker == SyncSource.Portal)).Should().Be(2); // create + comment
    }
}
