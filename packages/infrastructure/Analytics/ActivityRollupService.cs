using Desk.Application.Analytics;
using Desk.Domain.Analytics;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Analytics;

/// <summary>
/// Rolls the activity log into daily facts and expires the raw rows behind them.
///
/// Runs under PLATFORM scope: one pass covers every tenant, and the facts it writes carry the
/// tenant of the events they came from, so the global filter still isolates them on read.
/// </summary>
public sealed class ActivityRollupService(DeskDbContext db, TimeProvider clock) : IActivityRollupService
{
    /// <summary>
    /// How far back each pass recomputes. PSA data arrives late — a closure observed today can carry
    /// a timestamp from days ago — so recent history has to be rebuilt rather than appended to.
    /// Seven days is comfortably longer than any sync lag seen here and still cheap.
    /// </summary>
    private const int RecomputeDays = 7;

    /// <summary>
    /// How long raw events are kept. Thirteen months so a year-on-year comparison still has the
    /// detail behind it; the facts themselves are kept indefinitely, because they are small.
    /// </summary>
    private const int RawRetentionDays = 396;

    /// <summary>Rows deleted per statement, and the ceiling on one pass. A large backlog drains
    /// across successive runs rather than holding the table for the length of one.</summary>
    private const int ExpiryBatchSize = 500;
    private const int MaxExpiryBatches = 40;

    public async Task<ActivityRollupResult> RunAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var firstDay = DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(-RecomputeDays));
        var windowStart = firstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Portal events name a portal user; the facts are keyed on the PSA identity so both halves
        // aggregate on the same axis. Someone unmapped resolves to null rather than a guess.
        var externalIdByUser = await db.UserPsaIdentities.IgnoreQueryFilters().AsNoTracking()
            .Select(i => new { i.AppUserId, i.ExternalTechnicianId })
            .ToListAsync(ct);
        var actorLookup = externalIdByUser
            .GroupBy(i => i.AppUserId)
            .ToDictionary(g => g.Key, g => g.First().ExternalTechnicianId);

        var events = await db.ActivityEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.OccurredAt >= windowStart)
            .Select(e => new
            {
                e.MspOrganizationId, e.OccurredAt, e.Source, e.ActorUserId, e.ActorExternalId,
                e.ClientCompanyId, e.DurationSeconds,
            })
            .ToListAsync(ct);

        // Delete-then-insert for the window. The alternative — a unique index and upserts — looks
        // tidier and is a trap here: three of the grain's columns are nullable, and Postgres treats
        // NULLs as distinct, so the index would not actually prevent the duplicates it appears to.
        var stale = db.ActivityDailyFacts.IgnoreQueryFilters().Where(f => f.Day >= firstDay);
        db.ActivityDailyFacts.RemoveRange(await stale.ToListAsync(ct));

        var facts = events
            .GroupBy(e => new
            {
                e.MspOrganizationId,
                Day = DateOnly.FromDateTime(e.OccurredAt.UtcDateTime.Date),
                e.Source,
                Actor = e.ActorExternalId
                    ?? (e.ActorUserId is { } uid ? actorLookup.GetValueOrDefault(uid) : null),
                e.ClientCompanyId,
            })
            .Select(g => new ActivityDailyFact
            {
                MspOrganizationId = g.Key.MspOrganizationId,
                Day = g.Key.Day,
                Source = g.Key.Source,
                ActorExternalId = g.Key.Actor,
                ClientCompanyId = g.Key.ClientCompanyId,
                EventCount = g.Count(),
                DurationSeconds = g.Sum(e => e.DurationSeconds ?? 0),
            })
            .ToList();

        db.ActivityDailyFacts.AddRange(facts);
        await db.SaveChangesAsync(ct);

        // Expire AFTER the rollup, never before: dropping events that had not been aggregated would
        // lose exactly the history the facts exist to preserve.
        //
        // Deleted in batches rather than one set-based statement. A single ExecuteDelete would be
        // faster, but it is unsupported by the in-memory provider the tests run on — and a path
        // that DELETES DATA is the last one to leave unverified. Batching also bounds how long any
        // single pass holds locks on a table the dashboards read.
        var horizon = now.AddDays(-RawRetentionDays);
        var expired = 0;
        for (var batch = 0; batch < MaxExpiryBatches; batch++)
        {
            var doomed = await db.ActivityEvents.IgnoreQueryFilters()
                .Where(e => e.OccurredAt < horizon)
                .Take(ExpiryBatchSize)
                .ToListAsync(ct);
            if (doomed.Count == 0) break;

            db.ActivityEvents.RemoveRange(doomed);
            await db.SaveChangesAsync(ct);
            expired += doomed.Count;
        }

        return new ActivityRollupResult(RecomputeDays + 1, facts.Count, expired);
    }
}
