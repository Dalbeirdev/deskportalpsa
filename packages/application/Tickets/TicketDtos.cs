using Desk.Domain.Enums;

namespace Desk.Application.Tickets;

/// <summary>The client identity a request acts on behalf of, resolved from the authenticated subject.</summary>
public sealed record ClientAccess(Guid MspOrganizationId, Guid ClientCompanyId, Guid ClientUserId, bool IsCompanyAdministrator);

public sealed record TicketListItem(
    Guid Id,
    string? ExternalTicketId,
    ProviderType Provider,
    string Title,
    string PortalStatus,
    string PortalPriority,
    string? QueueOrBoard,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSyncedAt,
    string? CustomerName,
    string? ConnectionName);

public sealed record TicketNoteDto(
    Guid Id,
    string AuthorName,
    bool AuthoredByClient,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>
/// Ticket detail for the client portal. Contains ONLY public conversation — internal PSA notes
/// are never loaded into this shape (they are not even persisted to the portal).
/// </summary>
public sealed record TicketDetailDto(
    Guid Id,
    string? ExternalTicketId,
    ProviderType Provider,
    string Title,
    string? Description,
    string PortalStatus,
    string PortalPriority,
    string? PortalCategory,
    string? QueueOrBoard,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<TicketNoteDto> Conversation,
    IReadOnlyList<AttachmentDto> Attachments,
    string? CustomerName,
    DateTimeOffset UpdatedAt,
    string? ConnectionName,
    // Ticket service instructions the client set for technicians to follow (account override
    // if present, otherwise the organization-wide default). Trailing + optional so existing
    // construction sites and tests are unaffected.
    string? ServiceInstructions = null);

public sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    AttachmentScanStatus ScanStatus,
    DateTimeOffset UploadedAt)
{
    /// <summary>Who attached it. Null for a portal upload by the ticket's own requester.</summary>
    public string? AuthorName { get; init; }

    /// <summary>True when the file came from the PSA rather than being uploaded here.</summary>
    public bool FromProvider { get; init; }

    /// <summary>Conversation entry this file was posted with, so the UI can show it in context.</summary>
    public Guid? TicketNoteId { get; init; }
}

public sealed record CreateTicketInput(
    string Title,
    string? Description,
    string? Priority,
    string? Category,
    string? QueueOrBoard);

public sealed record CreateTicketResultDto(Guid Id, string? ExternalTicketId);

public sealed record NotificationDto(
    Guid TicketId,
    string Title,
    string Kind,
    string Summary,
    DateTimeOffset At);
