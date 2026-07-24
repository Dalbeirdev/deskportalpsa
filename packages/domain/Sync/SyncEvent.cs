using Desk.Domain.Common;
using Desk.Domain.Enums;

namespace Desk.Domain.Sync;

/// <summary>
/// Record of a single inbound or outbound synchronization event. The unique
/// (PsaConnectionId, IdempotencyKey) constraint drops duplicate webhook/poll deliveries;
/// <see cref="SourceMarker"/> + <see cref="CorrelationId"/> break echo loops.
/// </summary>
public class SyncEvent : TenantEntity
{
    public Guid PsaConnectionId { get; set; }
    public Guid? TicketId { get; set; }

    public required string EventType { get; set; }

    /// <summary>Deterministic key per source event — the dedup guard.</summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>"portal" or "provider" — a write originated by us is ignored when it echoes back.</summary>
    public required string SourceMarker { get; set; }

    public Guid? CorrelationId { get; set; }
    public string? PayloadHash { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public bool Processed { get; set; }
    public string? Error { get; set; }
}

/// <summary>A unit of deferred/background work with retry + dead-letter tracking.</summary>
public class BackgroundJob : TenantEntity
{
    public required string JobType { get; set; }
    public required string PayloadJson { get; set; }
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Queued;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
