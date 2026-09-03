using Desk.Domain.Analytics;

namespace Desk.Application.Analytics;

/// <summary>One activity to record. Everything but kind and source is optional by design — a PSA
/// observation knows no portal user, and a portal action may have no PSA identity yet.</summary>
public sealed record ActivityRecord(ActivityKind Kind, ActivitySource Source)
{
    /// <summary>When it happened. Defaults to now at the recorder, never guessed by the caller.</summary>
    public DateTimeOffset? OccurredAt { get; init; }
    public Guid? MspOrganizationId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorExternalId { get; init; }
    public Guid? PsaConnectionId { get; init; }
    public Guid? TicketId { get; init; }
    public Guid? ClientCompanyId { get; init; }
    public int? DurationSeconds { get; init; }
    public string? Detail { get; init; }
}

/// <summary>
/// Appends to the activity log.
///
/// Append-only on purpose: there is no update and no delete, because a history that can be edited
/// answers nothing anyone would trust. Recording also MUST NOT be able to fail the thing it
/// observes — a reply that posted successfully must not report failure because its telemetry did.
/// </summary>
public interface IActivityRecorder
{
    /// <summary>Records one activity. Never throws.</summary>
    Task RecordAsync(ActivityRecord record, CancellationToken ct = default);

    /// <summary>
    /// Records several in one save. Used by the sync, which observes many tickets per run and
    /// should not pay a round trip each.
    /// </summary>
    Task RecordManyAsync(IReadOnlyList<ActivityRecord> records, CancellationToken ct = default);
}
