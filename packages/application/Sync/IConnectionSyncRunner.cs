namespace Desk.Application.Sync;

/// <summary>Tally of one inbound sync run.</summary>
/// <summary>Outcome of one inbound run. <paramref name="Notes"/> counts conversation entries
/// mirrored from the provider, so an admin can see note import actually doing something.</summary>
public sealed record SyncRunResult(int Fetched, int Created, int Updated, int Skipped, int Pages, int Notes = 0);

/// <summary>
/// Runs a full inbound sync for one PSA connection: pages tickets from the provider connector,
/// maps and upserts each into the portal projection, and updates the connection's health and
/// sync cursor. Used by the manual "sync now" trigger and can back a scheduled poll.
/// </summary>
public interface IConnectionSyncRunner
{
    /// <param name="full">
    /// Ignore the incremental cursor and re-pull every ticket. Use after changing field mappings so
    /// existing tickets are re-translated with the new rules (an incremental run would skip them,
    /// since nothing changed on the provider side).
    /// </param>
    Task<SyncRunResult> RunAsync(Guid psaConnectionId, bool full = false, CancellationToken ct = default);
}
