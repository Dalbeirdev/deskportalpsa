using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Application.Sync;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.PsaCore.Contracts;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Models;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Sync;

/// <summary>
/// Orchestrates a full inbound sync for one connection: resolve its connector, page tickets
/// (incremental from the last successful sync cursor), translate + upsert each via the sync engine,
/// then stamp the connection's health and cursor. On failure the connection is marked Degraded with
/// the error recorded, and the exception is rethrown for the caller to surface.
/// </summary>
public sealed class ConnectionSyncRunner(
    DeskDbContext db,
    IConnectorResolver resolver,
    ITicketSyncService sync,
    IObjectStorage storage,
    IMalwareScanner scanner,
    TimeProvider clock) : IConnectionSyncRunner
{
    private const int MaxPages = 50; // safety cap so a runaway cursor can't loop forever

    public async Task<SyncRunResult> RunAsync(Guid psaConnectionId, bool full = false, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == psaConnectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        // Two-way sync off means nothing flows back from the provider: portal → PSA writes still
        // happen, but an inbound run must not touch the projection.
        if (!connection.TwoWaySync)
            return new SyncRunResult(0, 0, 0, 0, 0);

        var connector = await resolver.ResolveAsync(psaConnectionId, ct);
        // Asked once per run, not per ticket: it decides whether time aggregates are worth pulling.
        var capabilities = await connector.GetCapabilitiesAsync(ct);
        var rules = await db.FieldMappings.AsNoTracking()
            .Where(m => m.Provider == connection.Provider && m.IsActive)
            .ToListAsync(ct);

        int fetched = 0, created = 0, updated = 0, skipped = 0, pages = 0, notes = 0, files = 0;
        string? cursor = null;
        try
        {
            do
            {
                var page = await connector.GetTicketsAsync(
                    new TicketFilter
                    {
                        ModifiedSince = full ? null : connection.LastSuccessfulSyncAt,
                        PageSize = 100,
                        Cursor = cursor,
                        CompanyIds = Csv(connection.FilterCompanyIds),
                        QueueOrBoardIds = Csv(connection.FilterQueueIds),
                        AssignedResourceIds = Csv(connection.FilterResourceIds),
                        IncludeClosed = connection.ImportClosedTickets,
                        ActiveWithinDays = connection.FilterActiveWithinDays,
                    }, ct);
                pages++;
                foreach (var ticket in page.Items)
                {
                    // Client-side guard: providers express filters differently (and some not at all),
                    // so re-apply them here to keep behaviour identical across connectors.
                    if (!Passes(connection, ticket)) { skipped++; continue; }
                    // Brand-new tickets are only created when auto-import is on; existing ones still update.
                    if (!connection.AutoImportNewTickets && !await KnownAsync(psaConnectionId, ticket.ExternalId, ct))
                    { skipped++; continue; }
                    fetched++;
                    switch (await sync.UpsertFromProviderAsync(psaConnectionId, ticket, rules, ct))
                    {
                        case TicketSyncOutcome.Created: created++; break;
                        case TicketSyncOutcome.Updated: updated++; break;
                        default: skipped++; break;
                    }

                    if (connection.ImportNotes)
                        notes += await ImportNotesAsync(connection, connector, ticket.ExternalId, ct);

                    // Time logged provider-side never reaches the portal's stored totals otherwise:
                    // they were only rewritten when time was logged from here, so a technician's own
                    // entry left the dashboards under-reporting.
                    //
                    // Keyed off the ticket being fetched at all, NOT off the upsert outcome: adding a
                    // time entry bumps the provider's activity date (so an incremental page returns
                    // the ticket) but changes none of the fields in the update hash, so the upsert
                    // reports "unchanged" and a stricter guard here skipped every refresh.
                    if (capabilities.SupportsTimeEntries)
                        await RefreshTimeTotalsAsync(psaConnectionId, connector, ticket.ExternalId, ct);
                }
                cursor = page.NextCursor;
                if (!page.HasMore) break;
            } while (cursor is not null && pages < MaxPages);

            // Attachments are swept separately, and deliberately outside the ticket loop: providers
            // do not reliably touch a ticket's modified timestamp when a file is attached, so an
            // incremental ticket page would miss them entirely. One dated query covers the tenant.
            if (connection.SyncAttachments && capabilities.SupportsAttachmentDownload)
                files = await ImportAttachmentsAsync(connection, connector, full ? null : connection.LastSuccessfulSyncAt, ct);

            connection.LastSuccessfulSyncAt = clock.GetUtcNow();
            connection.LastHealthCheckAt = clock.GetUtcNow();
            connection.Status = ConnectionStatus.Healthy;
            connection.LastError = null;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            connection.Status = ConnectionStatus.Degraded;
            connection.LastError = ex.Message;
            connection.LastHealthCheckAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            throw;
        }

        return new SyncRunResult(fetched, created, updated, skipped, pages, notes, files);
    }

    /// <summary>
    /// Mirrors the provider's PUBLIC notes into the portal thread. Deduplication is by the provider's
    /// own note id, which doubles as echo suppression: a reply written in the portal already stored
    /// that id when the provider accepted it, so it is recognised rather than duplicated.
    /// Internal notes never reach here — the connector's GetPublicNotesAsync filters them out, so a
    /// private note cannot leak into a customer-visible thread.
    /// </summary>
    private async Task<int> ImportNotesAsync(PsaConnection connection, IServiceManagementConnector connector, string externalTicketId, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(
            t => t.PsaConnectionId == connection.Id && t.ExternalTicketId == externalTicketId, ct);
        if (ticket is null) return 0;

        IReadOnlyList<UnifiedTicketNote> incoming;
        try { incoming = await connector.GetPublicNotesAsync(externalTicketId, ct); }
        catch (ConnectorException) { return 0; } // one ticket's notes must not fail the whole run

        var existing = await db.TicketNotes
            .Where(n => n.TicketId == ticket.Id && n.ExternalNoteId != null)
            .Select(n => n.ExternalNoteId!)
            .ToListAsync(ct);
        var known = new HashSet<string>(existing);

        var added = 0;
        foreach (var n in incoming)
        {
            if (string.IsNullOrEmpty(n.ExternalId) || known.Contains(n.ExternalId)) continue;
            // Machine-generated notes have no human author; skip unless explicitly wanted.
            if (!connection.ImportSystemNotes && string.IsNullOrWhiteSpace(n.AuthorName)) continue;

            db.TicketNotes.Add(new TicketNote
            {
                MspOrganizationId = ticket.MspOrganizationId,
                TicketId = ticket.Id,
                ExternalNoteId = n.ExternalId,
                // An empty author means the provider generated the note itself (workflow/SLA); name it
                // after the provider rather than leaving a blank byline in the thread.
                AuthorName = string.IsNullOrWhiteSpace(n.AuthorName) ? $"{connection.Provider} automation" : n.AuthorName,
                AuthoredByClient = false,
                Body = n.Body,
                IsPublic = true,
                NoteCreatedAt = n.CreatedAt,
            });
            known.Add(n.ExternalId);
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>Rewrites one ticket's worked/billable totals from the PSA, which owns the truth.</summary>
    private async Task RefreshTimeTotalsAsync(Guid connectionId, IServiceManagementConnector connector, string externalTicketId, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(
            t => t.PsaConnectionId == connectionId && t.ExternalTicketId == externalTicketId, ct);
        if (ticket is null) return;

        IReadOnlyList<UnifiedTimeEntry> entries;
        try { entries = await connector.GetTimeEntriesAsync(externalTicketId, ct); }
        catch (ConnectorException) { return; } // one ticket's time must not fail the whole run

        ticket.TimeWorkedHours = entries.Sum(e => e.Hours);
        ticket.BillableHours = entries.Where(e => e.Billable).Sum(e => e.Hours);
        ticket.NonBillableHours = entries.Where(e => !e.Billable).Sum(e => e.Hours);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Mirrors the provider's attachments into the portal, bytes included. Deduplication is by the
    /// provider's own attachment id, which — exactly as with notes — also suppresses the echo of a
    /// portal upload that was already pushed out and recorded with that id.
    ///
    /// Imported bytes are scanned before they are stored, on the same footing as a customer upload:
    /// a PSA is not a trusted source, and a technician can attach anything to a ticket.
    /// </summary>
    private async Task<int> ImportAttachmentsAsync(PsaConnection connection, IServiceManagementConnector connector, DateTimeOffset? since, CancellationToken ct)
    {
        IReadOnlyList<ProviderAttachmentRef> incoming;
        try { incoming = await connector.GetRecentAttachmentsAsync(since, ct); }
        catch (ConnectorException) { return 0; } // files must not fail the whole run
        if (incoming.Count == 0) return 0;

        // Only tickets this connection already projects. A file on a ticket we do not import is not
        // ours to store, and the sweep is deliberately tenant-wide.
        var wanted = incoming.Select(r => r.TicketExternalId).Distinct().ToList();
        var tickets = await db.Tickets
            .Where(t => t.PsaConnectionId == connection.Id && t.ExternalTicketId != null && wanted.Contains(t.ExternalTicketId))
            .Select(t => new { t.Id, t.ExternalTicketId, t.MspOrganizationId })
            .ToListAsync(ct);
        if (tickets.Count == 0) return 0;
        var byExternalId = tickets.ToDictionary(t => t.ExternalTicketId!, t => t);

        var ticketIds = tickets.Select(t => t.Id).ToList();
        // Provider note id -> portal note, so an imported file lands under the reply it belongs to
        // instead of in an undifferentiated pile at the bottom of the ticket.
        var noteIdByExternalId = await db.TicketNotes
            .Where(n => ticketIds.Contains(n.TicketId) && n.ExternalNoteId != null)
            .ToDictionaryAsync(n => n.ExternalNoteId!, n => n.Id, ct);

        var known = new HashSet<string>(await db.TicketAttachments
            .Where(a => ticketIds.Contains(a.TicketId) && a.ExternalAttachmentId != null)
            .Select(a => a.ExternalAttachmentId!)
            .ToListAsync(ct));

        var added = 0;
        foreach (var (externalTicketId, file) in incoming.Select(r => (r.TicketExternalId, r.Attachment)))
        {
            if (string.IsNullOrEmpty(file.ExternalId) || known.Contains(file.ExternalId)) continue;
            if (!byExternalId.TryGetValue(externalTicketId, out var ticket)) continue;

            DownloadedAttachment? payload;
            try { payload = await connector.DownloadAttachmentAsync(externalTicketId, file.ExternalId, ct); }
            catch (ConnectorException) { continue; }
            // No bytes means the provider cannot serve this file. Recording metadata alone would put
            // an undownloadable row in the customer's list, so skip it and retry on the next run.
            if (payload is null || payload.Content.Length == 0) continue;

            var scan = await scanner.ScanAsync(payload.Content, payload.FileName, ct);
            var record = new TicketAttachment
            {
                MspOrganizationId = ticket.MspOrganizationId,
                TicketId = ticket.Id,
                ExternalAttachmentId = file.ExternalId,
                TicketNoteId = file.ExternalNoteId is { } n && noteIdByExternalId.TryGetValue(n, out var localNote)
                    ? localNote
                    : null,
                OriginalFileName = payload.FileName,
                ContentType = payload.ContentType,
                SizeBytes = payload.Content.LongLength,
                StorageObjectKey = string.Empty,
                UploadedAt = file.CreatedAt ?? clock.GetUtcNow(),
                AuthorName = string.IsNullOrWhiteSpace(file.AuthorName) ? $"{connection.Provider} automation" : file.AuthorName,
                ImportedFromProvider = true,
            };

            if (scan.IsClean)
            {
                var key = $"att/{ticket.Id}/{Guid.NewGuid():N}{Path.GetExtension(payload.FileName)}";
                await storage.PutAsync(key, payload.Content, payload.ContentType, ct);
                record.StorageObjectKey = key;
                record.ScanStatus = AttachmentScanStatus.Clean;
            }
            else
            {
                record.ScanStatus = AttachmentScanStatus.Quarantined;
                record.ScanDetail = scan.Detail;
            }

            db.TicketAttachments.Add(record);
            known.Add(file.ExternalId);
            added++;
        }

        if (added > 0) await db.SaveChangesAsync(ct);
        return added;
    }

    private static IReadOnlyList<string> Csv(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Re-applies the connection's import filters to a fetched ticket (provider-agnostic).</summary>
    private static bool Passes(PsaConnection c, UnifiedTicket t)
    {
        var closed = t.ClosedAt is not null || t.ResolvedAt is not null;
        if (closed && !c.ImportClosedTickets) return false;
        if (!closed && !c.ImportOpenTickets) return false;

        var queues = Csv(c.FilterQueueIds);
        if (queues.Count > 0 && !queues.Contains(t.QueueOrBoard ?? "", StringComparer.OrdinalIgnoreCase)) return false;

        var resources = Csv(c.FilterResourceIds);
        if (resources.Count > 0 && !resources.Contains(t.AssignedTechnicianExternalId ?? "", StringComparer.OrdinalIgnoreCase)) return false;

        var companies = Csv(c.FilterCompanyIds);
        if (companies.Count > 0 && !companies.Contains(t.RequesterExternalId ?? "", StringComparer.OrdinalIgnoreCase)) return false;

        if (c.FilterActiveWithinDays is > 0 and { } days)
        {
            var last = t.ModifiedAt ?? t.CreatedAt;
            if (last is not null && last < DateTimeOffset.UtcNow.AddDays(-days)) return false;
        }
        return true;
    }

    private Task<bool> KnownAsync(Guid connectionId, string externalId, CancellationToken ct)
        => db.Tickets.AnyAsync(t => t.PsaConnectionId == connectionId && t.ExternalTicketId == externalId, ct);
}
