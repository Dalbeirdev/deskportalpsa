using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Domain.Enums;
using Desk.Domain.Mapping;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Sync;
using Desk.PsaCore.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

public class TicketSyncTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    private static async Task<DeskDbContext> SeedConnectionAsync(string dbName)
    {
        var db = TestDbContextFactory.ForPlatform(dbName);
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static TicketSyncService Service(DeskDbContext db, TestClock clock, ISyncEventStore? events = null)
        => new(db, new MappingEngine(), events ?? new SyncEventStore(db, clock), clock, new RecordingActivity());

    private static UnifiedTicket Incoming(string extId, string title, string status) => new()
    {
        ExternalId = extId, Title = title, Status = status, Priority = "1",
        RequesterExternalId = "1", RequesterName = "Acme", RequesterEmail = "a@acme.test",
    };

    [Fact]
    public async Task First_sync_creates_the_ticket_and_the_client_company()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedConnectionAsync(dbName);

        var outcome = await Service(db, clock).UpsertFromProviderAsync(Conn, Incoming("500", "Printer", "5"), []);

        outcome.Should().Be(TicketSyncOutcome.Created);
        var ticket = await db.Tickets.SingleAsync();
        ticket.ExternalTicketId.Should().Be("500");
        ticket.PsaStatus.Should().Be("5");
        (await db.ClientCompanies.CountAsync()).Should().Be(1); // company auto-created
    }

    [Fact]
    public async Task Resync_with_no_changes_is_skipped_as_unchanged()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedConnectionAsync(dbName);
        var svc = Service(db, clock);

        await svc.UpsertFromProviderAsync(Conn, Incoming("500", "Printer", "5"), []);
        var second = await svc.UpsertFromProviderAsync(Conn, Incoming("500", "Printer", "5"), []);

        second.Should().Be(TicketSyncOutcome.SkippedUnchanged);
    }

    [Fact]
    public async Task Changed_ticket_updates_in_place()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedConnectionAsync(dbName);
        var svc = Service(db, clock);

        await svc.UpsertFromProviderAsync(Conn, Incoming("500", "Printer", "5"), []);
        var outcome = await svc.UpsertFromProviderAsync(Conn, Incoming("500", "Printer jammed", "1"), []);

        outcome.Should().Be(TicketSyncOutcome.Updated);
        (await db.Tickets.SingleAsync()).Title.Should().Be("Printer jammed");
    }

    [Fact]
    public async Task Inbound_status_is_translated_by_the_mapping_rules()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedConnectionAsync(dbName);

        var rules = new List<FieldMapping>
        {
            new()
            {
                MspOrganizationId = Org, Provider = ProviderType.AutotaskPsa, Scope = MappingScope.ProviderDefault,
                PortalField = "status", ExternalField = "status", PortalValue = "RESOLVED", ExternalValue = "5",
                Direction = MappingDirection.Bidirectional,
            },
        };

        await Service(db, clock).UpsertFromProviderAsync(Conn, Incoming("500", "Printer", "5"), rules);

        var ticket = await db.Tickets.SingleAsync();
        ticket.PortalStatus.Should().Be("RESOLVED"); // translated
        ticket.PsaStatus.Should().Be("5");           // raw preserved
    }

    [Fact]
    public async Task Portal_originated_change_is_skipped_as_echo()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedConnectionAsync(dbName);
        var events = new SyncEventStore(db, clock);
        var svc = Service(db, clock, events);

        // Establish the ticket.
        await svc.UpsertFromProviderAsync(Conn, Incoming("500", "Printer", "5"), []);
        var ticket = await db.Tickets.SingleAsync();

        // Simulate the portal having just pushed the *next* state (status -> "1"), recording its hash.
        // Through the SAME canonical function production uses on both sides: a hand-rolled copy here
        // would pass while echo suppression was broken, which is exactly what it did.
        var portalHash = UpdateHasher.ForTicketState(
            status: "1", priority: "1", category: null, title: "Printer", description: null,
            resolvedAt: null, closedAt: null, slaDueAt: null);
        await events.TryRegisterAsync(new SyncEventRegistration
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, TicketId = ticket.Id,
            EventType = "ticket.updated", IdempotencyKey = "portal-x", SourceMarker = SyncSource.Portal,
            PayloadHash = portalHash, OccurredAt = clock.GetUtcNow(),
        });

        // The provider now echoes that same change back — must be skipped.
        var outcome = await svc.UpsertFromProviderAsync(Conn, Incoming("500", "Printer", "1"), []);
        outcome.Should().Be(TicketSyncOutcome.SkippedEcho);
    }
}
