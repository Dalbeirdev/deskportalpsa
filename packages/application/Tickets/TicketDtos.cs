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
    DateTimeOffset? LastSyncedAt);

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
    IReadOnlyList<AttachmentDto> Attachments);

public sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    AttachmentScanStatus ScanStatus,
    DateTimeOffset UploadedAt);

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
