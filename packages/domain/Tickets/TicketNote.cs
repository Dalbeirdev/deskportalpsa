using Desk.Domain.Common;

namespace Desk.Domain.Tickets;

/// <summary>
/// A ticket note. Only PUBLIC notes are ever mirrored into the portal and shown to clients;
/// internal PSA notes must never reach this table (spec §7/§9: internal notes stay hidden).
/// </summary>
public class TicketNote : TenantEntity
{
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string? ExternalNoteId { get; set; }
    public required string AuthorName { get; set; }
    public bool AuthoredByClient { get; set; }
    public required string Body { get; set; }

    /// <summary>Invariant: always true for persisted notes. Private notes are filtered before persistence.</summary>
    public bool IsPublic { get; set; } = true;

    public DateTimeOffset NoteCreatedAt { get; set; }

    /// <summary>Set when this note originated from the portal, so the inbound sync can skip its own echo.</summary>
    public Guid? OriginCorrelationId { get; set; }
}
