using Desk.Application.Attachments;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The portal's own record of time and files: which system an entry came from, whether it reached
/// the PSA, and which reply a file belongs to — none of which the PSA can answer on its own.
/// </summary>
public class TimeEntryRecordTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    private static async Task<(AdminHarness H, Guid TicketId, Guid NoteId, StubConnector Connector, AttachmentService Svc)> SetupAsync()
    {
        var h = AdminHarness.Create(Org);
        h.Db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x", SyncAttachments = true,
        });
        var ticket = new Ticket
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.AutotaskPsa,
            ExternalTicketId = "7810", ClientCompanyId = Guid.NewGuid(),
            RequesterName = "R", RequesterEmail = "r@x", Title = "t",
            PortalStatus = "NEW", PortalPriority = "NORMAL",
        };
        h.Db.Tickets.Add(ticket);
        var note = new TicketNote
        {
            MspOrganizationId = Org, TicketId = ticket.Id, ExternalNoteId = "29683540",
            AuthorName = "Demo Admin", AuthoredByClient = true, Body = "Log attached.",
            IsPublic = true, NoteCreatedAt = h.Clock.GetUtcNow(),
        };
        h.Db.TicketNotes.Add(note);
        await h.Db.SaveChangesAsync();

        var connector = new StubConnector();
        var svc = new AttachmentService(h.Db, new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock),
            new HeuristicMalwareScanner(), new FakeResolver(connector),
            new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), new AttachmentPolicy(), h.Clock);
        return (h, ticket.Id, note.Id, connector, svc);
    }

    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2];

    [Fact]
    public async Task A_file_posted_with_a_reply_records_the_note_on_both_sides()
    {
        var (h, ticketId, noteId, connector, svc) = await SetupAsync();
        await using var _ = h.Db;

        await svc.UploadAsync(new UploadAttachmentInput(ticketId, Org, "log.txt", "text/plain", Png, noteId));

        var row = await h.Db.TicketAttachments.SingleAsync();
        row.TicketNoteId.Should().Be(noteId); // the portal's own link, which drives the inline display
        // and the provider is told which note it belongs to, using the provider's id for that note.
        connector.Uploaded.Should().ContainSingle().Which.Attachment.ExternalNoteId.Should().Be("29683540");
    }

    [Fact]
    public async Task A_standalone_upload_carries_no_note_and_falls_through_to_the_loose_list()
    {
        var (h, ticketId, _, connector, svc) = await SetupAsync();
        await using var __ = h.Db;

        await svc.UploadAsync(new UploadAttachmentInput(ticketId, Org, "loose.png", "image/png", Png));

        (await h.Db.TicketAttachments.SingleAsync()).TicketNoteId.Should().BeNull();
        connector.Uploaded.Should().ContainSingle().Which.Attachment.ExternalNoteId.Should().BeNull();
    }

    [Fact]
    public async Task A_time_entry_records_its_origin_and_provider_id()
    {
        var h = AdminHarness.Create(Org);
        await using var _ = h.Db;
        var ticketId = Guid.NewGuid();
        h.Db.TicketTimeEntries.Add(new TicketTimeEntry
        {
            MspOrganizationId = Org, TicketId = ticketId, Hours = 0.5m, Billable = true,
            ExternalEntryId = "105", Source = TimeEntrySource.Portal, SyncStatus = TimeEntrySyncStatus.Synced,
            EntryDate = h.Clock.GetUtcNow(),
        });
        await h.Db.SaveChangesAsync();

        var row = await h.Db.TicketTimeEntries.SingleAsync();
        row.Source.Should().Be(TimeEntrySource.Portal);
        row.SyncStatus.Should().Be(TimeEntrySyncStatus.Synced);
        row.ExternalEntryId.Should().Be("105");
    }

    [Fact]
    public async Task A_retried_entry_reuses_what_was_already_logged()
    {
        var h = AdminHarness.Create(Org);
        await using var _ = h.Db;
        // Retry must re-send the original values, not ask for them again: the point is to recover
        // work already typed once, after the cause of the rejection is fixed.
        var entry = new TicketTimeEntry
        {
            MspOrganizationId = Org, TicketId = Guid.NewGuid(), Hours = 0.2m, Billable = true,
            Notes = "Diagnosed the switch port", WorkTypeId = "29682801", WorkRoleId = "29683355",
            Source = TimeEntrySource.Portal, SyncStatus = TimeEntrySyncStatus.Failed,
            SyncError = "no technician configured", EntryDate = h.Clock.GetUtcNow(),
        };
        h.Db.TicketTimeEntries.Add(entry);
        await h.Db.SaveChangesAsync();

        // What a successful retry stamps on the row.
        entry.ExternalEntryId = "111";
        entry.SyncStatus = TimeEntrySyncStatus.Synced;
        entry.SyncError = null;
        await h.Db.SaveChangesAsync();

        var row = await h.Db.TicketTimeEntries.SingleAsync();
        row.Hours.Should().Be(0.2m);
        row.Notes.Should().Be("Diagnosed the switch port");
        row.WorkTypeId.Should().Be("29682801");
        row.Source.Should().Be(TimeEntrySource.Portal); // origin survives the retry
        row.SyncError.Should().BeNull();
    }

    [Fact]
    public async Task A_rejected_time_entry_survives_with_its_reason()
    {
        var h = AdminHarness.Create(Org);
        await using var _ = h.Db;
        // The whole point of the table: before it, a rejected push 400'd and left no trace at all,
        // so the technician's logged work vanished with nothing to retry from.
        h.Db.TicketTimeEntries.Add(new TicketTimeEntry
        {
            MspOrganizationId = Org, TicketId = Guid.NewGuid(), Hours = 0.2m, Billable = true,
            Source = TimeEntrySource.Portal, SyncStatus = TimeEntrySyncStatus.Failed,
            SyncError = "Autotask needs a technician to own the time entry.",
            EntryDate = h.Clock.GetUtcNow(),
        });
        await h.Db.SaveChangesAsync();

        var row = await h.Db.TicketTimeEntries.SingleAsync();
        row.ExternalEntryId.Should().BeNull();
        row.SyncStatus.Should().Be(TimeEntrySyncStatus.Failed);
        row.SyncError.Should().NotBeNullOrWhiteSpace();
    }
}
