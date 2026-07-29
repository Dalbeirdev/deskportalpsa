using Desk.Application.Admin;
using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Application.Tickets;
using Desk.Domain.Enums;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Attachments;

/// <summary>
/// Attachment pipeline: validate → scan → store (clean) or quarantine (infected) → record + audit.
/// Infected content is never written to object storage; a quarantine record is kept for the audit
/// trail. Storage keys are randomized (never derived from the uploaded file name), and downloads are
/// only ever issued for CLEAN attachments via a short-lived signed URL.
/// </summary>
public sealed class AttachmentService(
    DeskDbContext db,
    IObjectStorage storage,
    IMalwareScanner scanner,
    IAuditWriter audit,
    AttachmentPolicy policy,
    TimeProvider clock) : IAttachmentService
{
    public async Task<AttachmentDto> UploadAsync(UploadAttachmentInput input, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == input.TicketId, ct)
            ?? throw new NotFoundException("Ticket");

        if (policy.Validate(input.FileName, input.ContentType, input.Content.LongLength) is { } reason)
            throw new ValidationFailedException(reason);

        var scan = await scanner.ScanAsync(input.Content, input.FileName, ct);

        var ext = Path.GetExtension(input.FileName);
        // Randomized key — never derived from the original file name.
        var storageKey = $"att/{ticket.Id}/{Guid.NewGuid():N}{ext}";

        var attachment = new TicketAttachment
        {
            MspOrganizationId = input.MspOrganizationId,
            TicketId = ticket.Id,
            OriginalFileName = input.FileName,
            ContentType = input.ContentType,
            SizeBytes = input.Content.LongLength,
            StorageObjectKey = string.Empty, // set below only if the scan is clean
            UploadedAt = clock.GetUtcNow(),
        };

        if (scan.IsClean)
        {
            await storage.PutAsync(storageKey, input.Content, input.ContentType, ct);
            attachment.StorageObjectKey = storageKey;
            attachment.ScanStatus = AttachmentScanStatus.Clean;
        }
        else
        {
            // Do not persist the bytes; keep only a quarantine record.
            attachment.StorageObjectKey = string.Empty;
            attachment.ScanStatus = AttachmentScanStatus.Quarantined;
            attachment.ScanDetail = scan.Detail;
        }

        db.TicketAttachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(
            scan.IsClean ? "attachment.uploaded" : "attachment.quarantined",
            "TicketAttachment", attachment.Id.ToString(),
            new { attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes, scan.Detail }, ct);

        return Dto(attachment);
    }

    public async Task<string?> GetDownloadUrlAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.TicketAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment is null
            || attachment.ScanStatus != AttachmentScanStatus.Clean
            || string.IsNullOrEmpty(attachment.StorageObjectKey))
            return null; // only clean, stored attachments are downloadable

        var url = await storage.PresignGetAsync(attachment.StorageObjectKey, policy.DownloadUrlTtl, ct);
        await audit.WriteAsync("attachment.downloaded", "TicketAttachment", attachment.Id.ToString(),
            new { attachment.OriginalFileName }, ct);
        return url;
    }

    private static AttachmentDto Dto(TicketAttachment a) =>
        new(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.ScanStatus, a.UploadedAt);
}
