namespace Desk.Application.Sync;

/// <summary>Tally of one inbound sync run.</summary>
public sealed record SyncRunResult(int Fetched, int Created, int Updated, int Skipped, int Pages);

/// <summary>
/// Runs a full inbound sync for one PSA connection: pages tickets from the provider connector,
/// maps and upserts each into the portal projection, and updates the connection's health and
/// sync cursor. Used by the manual "sync now" trigger and can back a scheduled poll.
/// </summary>
public interface IConnectionSyncRunner
{
    Task<SyncRunResult> RunAsync(Guid psaConnectionId, CancellationToken ct = default);
}
