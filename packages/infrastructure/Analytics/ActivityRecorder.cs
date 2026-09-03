using Desk.Application.Abstractions;
using Desk.Application.Analytics;
using Desk.Domain.Analytics;
using Desk.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Desk.Infrastructure.Analytics;

/// <summary>
/// Appends activity, and never lets doing so break the thing it observed.
///
/// Every method swallows its own failures and logs them. That is deliberate and worth being
/// explicit about: this is telemetry. A technician's reply reaching the PSA is the real work; if
/// the row recording that fact cannot be written, the reply still happened and the person must not
/// be told otherwise. The cost is that a database problem here is silent to the user — hence the
/// logging, which is where an operator would look.
/// </summary>
public sealed class ActivityRecorder(
    DeskDbContext db, ITenantContext tenant, TimeProvider clock, ILogger<ActivityRecorder> log)
    : IActivityRecorder
{
    public Task RecordAsync(ActivityRecord record, CancellationToken ct = default)
        => RecordManyAsync([record], ct);

    public async Task RecordManyAsync(IReadOnlyList<ActivityRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        try
        {
            var now = clock.GetUtcNow();
            foreach (var r in records)
            {
                // An event with no tenant cannot be read back through the global filter, so it
                // would be written and then invisible — worse than not written, because the row
                // still costs storage and nothing ever reports it missing.
                var org = r.MspOrganizationId ?? tenant.OrganizationId;
                if (org is not { } orgId) continue;

                db.ActivityEvents.Add(new ActivityEvent
                {
                    MspOrganizationId = orgId,
                    OccurredAt = r.OccurredAt ?? now,
                    Source = r.Source,
                    Kind = r.Kind,
                    ActorUserId = r.ActorUserId,
                    ActorExternalId = r.ActorExternalId,
                    PsaConnectionId = r.PsaConnectionId,
                    TicketId = r.TicketId,
                    ClientCompanyId = r.ClientCompanyId,
                    DurationSeconds = r.DurationSeconds,
                    Detail = Trim(r.Detail),
                });
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Activity not recorded ({Count} events). The observed action was unaffected.",
                records.Count);
        }
    }

    /// <summary>Detail is for a human reading the feed, so a pasted knowledge-base article would be
    /// storage spent on something nobody scrolls. Metrics never read it either way.</summary>
    private static string? Trim(string? detail)
        => string.IsNullOrWhiteSpace(detail) ? null
            : detail.Length <= 400 ? detail : detail[..400] + "…";
}
