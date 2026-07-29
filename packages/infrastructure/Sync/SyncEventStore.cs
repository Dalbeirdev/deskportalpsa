using Desk.Application.Sync;
using Desk.Domain.Sync;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Sync;

/// <summary>
/// DbContext-backed sync-event store. Uses a check-then-insert guarded by the unique
/// (PsaConnectionId, IdempotencyKey) index — the query handles the common case and the index
/// is the race backstop (a concurrent duplicate surfaces as a DbUpdateException → treated as dup).
/// </summary>
public sealed class SyncEventStore(DeskDbContext db, TimeProvider clock) : ISyncEventStore
{
    public async Task<bool> TryRegisterAsync(SyncEventRegistration reg, CancellationToken ct = default)
    {
        var already = await db.SyncEvents
            .AnyAsync(e => e.PsaConnectionId == reg.PsaConnectionId && e.IdempotencyKey == reg.IdempotencyKey, ct);
        if (already) return false;

        db.SyncEvents.Add(new SyncEvent
        {
            MspOrganizationId = reg.MspOrganizationId,
            PsaConnectionId = reg.PsaConnectionId,
            TicketId = reg.TicketId,
            EventType = reg.EventType,
            IdempotencyKey = reg.IdempotencyKey,
            SourceMarker = reg.SourceMarker,
            CorrelationId = reg.CorrelationId,
            PayloadHash = reg.PayloadHash,
            OccurredAt = reg.OccurredAt,
            Processed = false,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost the race — another worker registered the same event first.
            return false;
        }
    }

    public async Task<bool> IsPortalEchoAsync(Guid psaConnectionId, Guid ticketId, string payloadHash, CancellationToken ct = default)
    {
        // Look back a short window for a matching portal-origin change on this ticket.
        var cutoff = clock.GetUtcNow().AddMinutes(-10);
        return await db.SyncEvents.AnyAsync(e =>
            e.PsaConnectionId == psaConnectionId
            && e.TicketId == ticketId
            && e.SourceMarker == SyncSource.Portal
            && e.PayloadHash == payloadHash
            && e.OccurredAt >= cutoff, ct);
    }
}
