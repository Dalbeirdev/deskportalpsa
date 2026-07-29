using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Application.Sync;
using Desk.Domain.Enums;
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

    public async Task<SyncRunResult> RunAsync(Guid psaConnectionId, CancellationToken ct = default)
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
                    new TicketFilter { ModifiedSince = connection.LastSuccessfulSyncAt, PageSize = 100, Cursor = cursor }, ct);
                pages++;
                foreach (var ticket in page.Items)
                {
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
}
