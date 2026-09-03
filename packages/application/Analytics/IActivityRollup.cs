namespace Desk.Application.Analytics;

/// <summary>What one rollup pass did, so an operator can see it working rather than infer it.</summary>
public sealed record ActivityRollupResult(int DaysProcessed, int FactsWritten, int RawEventsExpired);

/// <summary>
/// Turns the raw activity log into daily facts, and keeps the raw log from growing without bound.
///
/// Both halves run together and in that order deliberately: expiring events that had not yet been
/// rolled up would silently lose the history the facts exist to preserve.
/// </summary>
public interface IActivityRollupService
{
    /// <summary>
    /// Recomputes the recent window and expires raw events past the retention horizon.
    ///
    /// Recomputes rather than appends because PSA data arrives late — a closure observed today can
    /// carry a timestamp from three days ago, and a rollup that only ever added would never correct
    /// the day it belonged to.
    /// </summary>
    Task<ActivityRollupResult> RunAsync(CancellationToken ct = default);
}
