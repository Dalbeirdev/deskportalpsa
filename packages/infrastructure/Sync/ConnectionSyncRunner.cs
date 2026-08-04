using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Application.Sync;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
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
    TimeProvider clock) : IConnectionSyncRunner
{
    private const int MaxPages = 50; // safety cap so a runaway cursor can't loop forever

    public async Task<SyncRunResult> RunAsync(Guid psaConnectionId, bool full = false, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == psaConnectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        var connector = await resolver.ResolveAsync(psaConnectionId, ct);
        var rules = await db.FieldMappings.AsNoTracking()
            .Where(m => m.Provider == connection.Provider && m.IsActive)
            .ToListAsync(ct);

        int fetched = 0, created = 0, updated = 0, skipped = 0, pages = 0;
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
                }
                cursor = page.NextCursor;
                if (!page.HasMore) break;
            } while (cursor is not null && pages < MaxPages);

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

        return new SyncRunResult(fetched, created, updated, skipped, pages);
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
