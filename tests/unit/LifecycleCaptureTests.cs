using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Connectors.Mock;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Sync;
using Desk.PsaCore.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The dates every completion metric is built on. None of them were being persisted: the columns
/// existed and the metrics service read them, but the sync wrote only ResolvedAt — so tickets
/// closed, resolution time and SLA performance were computed over an empty set, and ticket age was
/// measured from the day the portal happened to import the ticket.
/// </summary>
public class LifecycleCaptureTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    private static async Task<DeskDbContext> SeedAsync()
    {
        var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static TicketSyncService Service(DeskDbContext db, TestClock clock)
        => new(db, new MappingEngine(), new SyncEventStore(db, clock), clock);

    private static UnifiedTicket Incoming(
        DateTimeOffset? created = null, DateTimeOffset? resolved = null,
        DateTimeOffset? closed = null, DateTimeOffset? slaDue = null, string status = "New") => new()
        {
            ExternalId = "7814",
            Title = "Email Issue",
            Status = status,
            RequesterExternalId = "179",
            CreatedAt = created,
            ResolvedAt = resolved,
            ClosedAt = closed,
            SlaDueAt = slaDue,
        };

    [Fact]
    public async Task The_lifecycle_dates_reach_the_portal()
    {
        var clock = new TestClock();
        await using var db = await SeedAsync();
        var raised = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var due = new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
        var closed = new DateTimeOffset(2026, 6, 3, 15, 30, 0, TimeSpan.Zero);

        await Service(db, clock).UpsertFromProviderAsync(Conn,
            Incoming(created: raised, resolved: closed, closed: closed, slaDue: due), []);

        var t = await db.Tickets.SingleAsync();
        t.PsaCreatedAt.Should().Be(raised);
        t.SlaDueAt.Should().Be(due, "SLA performance is uncomputable without the target");
        t.ClosedAt.Should().Be(closed, "every throughput metric counts closures");
        t.ResolvedAt.Should().Be(closed);
    }

    [Fact]
    public async Task Ticket_age_is_measured_from_the_psa_raise_date_not_the_import_date()
    {
        // The insidious one: the row's own CreatedAt is when the portal first saw the ticket. A
        // ticket raised in June and imported in September is three months old, not new.
        var clock = new TestClock();
        await using var db = await SeedAsync();
        var raised = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

        await Service(db, clock).UpsertFromProviderAsync(Conn, Incoming(created: raised), []);

        var t = await db.Tickets.SingleAsync();
        t.PsaCreatedAt.Should().Be(raised);
        t.CreatedAt.Should().NotBe(raised, "the row timestamp is the import, and stays that");
    }

    [Fact]
    public async Task A_provider_that_gives_no_raise_date_leaves_it_null_rather_than_inventing_one()
    {
        // Defaulting to "now" would make an unknown-age ticket look brand new — worse than absent,
        // because a null can be excluded from an average and a wrong date cannot.
        var clock = new TestClock();
        await using var db = await SeedAsync();

        await Service(db, clock).UpsertFromProviderAsync(Conn, Incoming(created: null), []);

        (await db.Tickets.SingleAsync()).PsaCreatedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_closure_alone_still_updates_the_ticket()
    {
        // A ticket can close in the PSA with every other field identical. The update hash decides
        // whether the sync bothers to write, so a hash blind to closure dates means the portal
        // never records the closure at all.
        var clock = new TestClock();
        await using var db = await SeedAsync();
        var svc = Service(db, clock);
        var raised = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        await svc.UpsertFromProviderAsync(Conn, Incoming(created: raised), []);

        var closed = raised.AddDays(2);
        var outcome = await svc.UpsertFromProviderAsync(Conn,
            Incoming(created: raised, resolved: closed, closed: closed), []);

        outcome.Should().Be(TicketSyncOutcome.Updated, "the closure is a change worth persisting");
        (await db.Tickets.SingleAsync()).ClosedAt.Should().Be(closed);
    }
}
