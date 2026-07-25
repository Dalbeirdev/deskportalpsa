using Desk.Domain.Mapping;
using Desk.PsaCore.Models;

namespace Desk.Application.Sync;

public enum TicketSyncOutcome { Created, Updated, SkippedUnchanged, SkippedEcho }

/// <summary>
/// Applies normalized provider tickets to the portal's projection. Runs under the tenant scope of
/// the connection. Guards against redundant work (unchanged update hash) and against echoes of the
/// portal's own writes, so bidirectional sync never loops.
/// </summary>
public interface ITicketSyncService
{
    /// <summary>
    /// Upserts one provider ticket into the portal, translating provider values to portal values
    /// via <paramref name="mappingRules"/>. Ensures the owning client company exists first.
    /// </summary>
    Task<TicketSyncOutcome> UpsertFromProviderAsync(
        Guid psaConnectionId,
        UnifiedTicket ticket,
        IReadOnlyList<FieldMapping> mappingRules,
        CancellationToken ct = default);
}
