using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Sync;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Inbound conversation sync: provider notes must reach the portal thread exactly once, keep their
/// real author, and never carry a provider-side internal note into a customer-visible conversation.
/// </summary>
public class NoteImportTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    private static async Task<DeskDbContext> SeedAsync(string dbName, bool importNotes = true, bool importSystemNotes = false)
    {
        var db = TestDbContextFactory.ForPlatform(dbName);
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
            ImportNotes = importNotes, ImportSystemNotes = importSystemNotes,
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static ConnectionSyncRunner Runner(DeskDbContext db, StubConnector connector, TestClock clock)
        => new(db, new FakeResolver(connector), new TicketSyncService(db, new MappingEngine(), new SyncEventStore(db, clock), clock), clock);

    private static UnifiedTicket Incoming(string extId) => new()
    {
        ExternalId = extId, Title = "Network issue", Status = "1", Priority = "1",
        RequesterExternalId = "176", RequesterName = "Acme", RequesterEmail = "a@acme.test",
    };

    private static UnifiedTicketNote Note(string id, string author, string body, DateTimeOffset at) =>
        new(id, author, body, IsPublic: true, at);

    [Fact]
    public async Task Provider_notes_are_imported_once_and_keep_their_author()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedAsync(dbName);
        var connector = new StubConnector();
        connector.Tickets.Add(Incoming("7809"));
        connector.Notes["7809"] =
        [
            Note("29683530", "Jane Tech", "Applied a fix on the switch port.", clock.GetUtcNow()),
            Note("29683533", "Demo Admin", "Confirmed, thank you.", clock.GetUtcNow().AddMinutes(1)),
        ];

        var first = await Runner(db, connector, clock).RunAsync(Conn, full: true);
        var second = await Runner(db, connector, clock).RunAsync(Conn, full: true);

        first.Notes.Should().Be(2);
        second.Notes.Should().Be(0); // re-running must not duplicate the thread
        var notes = await db.TicketNotes.OrderBy(n => n.NoteCreatedAt).ToListAsync();
        notes.Select(n => n.AuthorName).Should().Equal("Jane Tech", "Demo Admin");
        notes.Should().OnlyContain(n => n.IsPublic && !n.AuthoredByClient);
    }

    [Fact]
    public async Task A_reply_written_in_the_portal_is_not_re_imported_as_an_echo()
    {
        var dbName = Guid.NewGuid().ToString();
        var clock = new TestClock();
        await using var db = await SeedAsync(dbName);
        var connector = new StubConnector();
        connector.Tickets.Add(Incoming("7809"));
        // Seed the projection the way the portal does after the provider accepts an outbound reply:
        // the note is stored locally already carrying the provider's own note id.
        await Runner(db, connector, clock).RunAsync(Conn, full: true);
        var ticket = await db.Tickets.SingleAsync();
        db.TicketNotes.Add(new TicketNote
        {
            MspOrganizationId = Org, TicketId = ticket.Id, ExternalNoteId = "29683533",
            AuthorName = "Demo Admin", AuthoredByClient = true, Body = "Portal reply.",
            IsPublic = true, NoteCreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
        connector.Notes["7809"] = [Note("29683533", "Desk Portal", "Portal reply.", clock.GetUtcNow())];

        var run = await Runner(db, connector, clock).RunAsync(Conn, full: true);

        run.Notes.Should().Be(0);
        var note = await db.TicketNotes.SingleAsync();
        note.AuthoredByClient.Should().BeTrue();   // the client's own byline survives the round trip
        note.AuthorName.Should().Be("Demo Admin");
    }

    [Fact]
    public async Task System_notes_are_skipped_unless_the_connection_asks_for_them()
    {
        var clock = new TestClock();
        var connector = new StubConnector();
        connector.Tickets.Add(Incoming("7809"));
        connector.Notes["7809"] =
        [
            Note("1", "Jane Tech", "Human reply.", clock.GetUtcNow()),
            Note("2", "", "SLA warning raised.", clock.GetUtcNow()), // no author = provider automation
        ];

        await using var off = await SeedAsync(Guid.NewGuid().ToString());
        (await Runner(off, connector, clock).RunAsync(Conn, full: true)).Notes.Should().Be(1);
        (await off.TicketNotes.Select(n => n.Body).ToListAsync()).Should().Equal("Human reply.");

        await using var on = await SeedAsync(Guid.NewGuid().ToString(), importSystemNotes: true);
        (await Runner(on, connector, clock).RunAsync(Conn, full: true)).Notes.Should().Be(2);
        (await on.TicketNotes.Where(n => n.Body == "SLA warning raised.").Select(n => n.AuthorName).SingleAsync())
            .Should().Be("AutotaskPsa automation");
    }

    [Fact]
    public async Task Note_import_is_skipped_entirely_when_the_connection_disables_it()
    {
        var clock = new TestClock();
        await using var db = await SeedAsync(Guid.NewGuid().ToString(), importNotes: false);
        var connector = new StubConnector();
        connector.Tickets.Add(Incoming("7809"));
        connector.Notes["7809"] = [Note("1", "Jane Tech", "Should not arrive.", clock.GetUtcNow())];

        var run = await Runner(db, connector, clock).RunAsync(Conn, full: true);

        run.Notes.Should().Be(0);
        connector.NoteReads.Should().Be(0); // and no wasted provider call
        (await db.TicketNotes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_failing_note_read_does_not_fail_the_ticket_sync()
    {
        var clock = new TestClock();
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var connector = new StubConnector { NoteReadFailure = new ConnectorException(ConnectorFailureKind.RateLimited, "429") };
        connector.Tickets.Add(Incoming("7809"));

        var run = await Runner(db, connector, clock).RunAsync(Conn, full: true);

        run.Created.Should().Be(1);
        run.Notes.Should().Be(0);
    }
}
