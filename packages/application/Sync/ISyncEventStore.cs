namespace Desk.Application.Sync;

/// <summary>Who originated a sync event. A "portal" write echoing back as a "provider" event is ignored.</summary>
public static class SyncSource
{
    public const string Portal = "portal";
    public const string Provider = "provider";
}

public sealed record SyncEventRegistration
{
    public required Guid MspOrganizationId { get; init; }
    public required Guid PsaConnectionId { get; init; }
    public Guid? TicketId { get; init; }
    public required string EventType { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string SourceMarker { get; init; }
    public Guid? CorrelationId { get; init; }
    public string? PayloadHash { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// Records sync events and enforces exactly-once processing. Duplicate deliveries (same
/// connection + idempotency key) and portal-originated echoes are rejected so sync never loops.
/// </summary>
public interface ISyncEventStore
{
    /// <summary>Registers an event; returns false if it is a duplicate and must be skipped.</summary>
    Task<bool> TryRegisterAsync(SyncEventRegistration registration, CancellationToken ct = default);

    /// <summary>
    /// True when an inbound provider event matches a recent portal-originated change on the same
    /// ticket (same payload hash) — i.e. our own write coming back. Such events are skipped.
    /// </summary>
    Task<bool> IsPortalEchoAsync(Guid psaConnectionId, Guid ticketId, string payloadHash, CancellationToken ct = default);
}
