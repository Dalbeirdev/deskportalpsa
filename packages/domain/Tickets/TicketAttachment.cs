using Desk.Domain.Common;
using Desk.Domain.Enums;

namespace Desk.Domain.Tickets;

/// <summary>
/// Attachment metadata only. Bytes live in object storage (MinIO/S3) under a randomized name;
/// access is always via a short-lived signed URL, never a direct path (spec §11 attachment security).
/// </summary>
public class TicketAttachment : TenantEntity
{
    public Guid TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string? ExternalAttachmentId { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    /// <summary>Randomized object-storage key — never derived from the original file name.</summary>
    public required string StorageObjectKey { get; set; }

    public AttachmentScanStatus ScanStatus { get; set; } = AttachmentScanStatus.Pending;
    public string? ScanDetail { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>Who attached it. Null for portal uploads, the provider-side name for imports.</summary>
    public string? AuthorName { get; set; }

    /// <summary>True when this row came from the provider rather than a portal upload.</summary>
    public bool ImportedFromProvider { get; set; }

    /// <summary>Set when a portal upload has been pushed to the provider, so it is never re-sent.</summary>
    public DateTimeOffset? PushedToProviderAt { get; set; }
}
