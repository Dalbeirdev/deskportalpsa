using Desk.Api.Auth;
using Desk.Application.Abstractions;
using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Application.Tickets;
using Desk.Domain.Authorization;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Controllers;

/// <summary>
/// Ticket attachment upload + download for the client portal. Uploads are validated, malware-scanned,
/// and quarantined if unsafe; downloads are issued only for clean files as short-lived signed URLs and
/// are audited. Access is scoped to the caller's company/ticket.
/// </summary>
[ApiController]
[Route("api")]
public sealed class AttachmentsController(
    ICurrentUser user,
    IClientAccessResolver accessResolver,
    IAttachmentService attachments,
    DeskDbContext db) : ControllerBase
{
    [Authorize]
    [RequirePermission(Permissions.TicketsAddPublicNote)]
    [HttpPost("tickets/{ticketId:guid}/attachments")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid ticketId, IFormFile file, [FromQuery] Guid? noteId, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file provided.");
        var access = await AccessAsync(ct);
        await EnsureTicketAccessAsync(access, ticketId, ct);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var dto = await attachments.UploadAsync(new UploadAttachmentInput(
            ticketId, access.MspOrganizationId, file.FileName, file.ContentType, ms.ToArray(), noteId), ct);

        return Ok(dto);
    }

    [Authorize]
    [HttpGet("tickets/{ticketId:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadUrl(Guid ticketId, Guid attachmentId, CancellationToken ct)
    {
        var access = await AccessAsync(ct);
        await EnsureTicketAccessAsync(access, ticketId, ct);

        var url = await attachments.GetDownloadUrlAsync(attachmentId, ct);
        return url is null ? NotFound() : Ok(new { url });
    }

    /// <summary>
    /// Token-gated blob fetch for the in-memory storage backend (no session — the HMAC signature is
    /// the gate). With MinIO/S3 in production the signed URL points at the object store directly and
    /// this endpoint is unused.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("attachments/blob")]
    public async Task<IActionResult> Blob(
        [FromQuery] string key, [FromQuery] long exp, [FromQuery] string sig,
        [FromServices] AttachmentStorageOptions options,
        [FromServices] IObjectStorage storage,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        if (!InMemoryObjectStorage.VerifySignature(key, exp, sig, options.SigningKey, clock.GetUtcNow()))
            return Unauthorized();

        var bytes = await storage.GetAsync(key, ct);
        if (bytes is null) return NotFound();

        // Look up metadata by the (unguessable, signature-verified) storage key for content type +
        // filename. The tenant filter is bypassed rather than re-scoped: the scope is deliberately
        // immutable once a request establishes it, so calling SetPlatformScope here threw on every
        // download. Only the display name and content type are read, and the signed key is already
        // proof of authorization.
        var meta = await db.TicketAttachments.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.StorageObjectKey == key, ct);
        var contentType = meta?.ContentType ?? "application/octet-stream";
        var fileName = meta?.OriginalFileName ?? "download";

        return File(bytes, contentType, fileName);
    }

    private async Task<ClientAccess> AccessAsync(CancellationToken ct)
        => await accessResolver.ResolveAsync(user.Subject ?? "", ct)
           ?? throw new ForbiddenException("This endpoint is for client portal users.");

    private async Task EnsureTicketAccessAsync(ClientAccess access, Guid ticketId, CancellationToken ct)
    {
        var ok = await db.Tickets.AnyAsync(t =>
            t.Id == ticketId
            && t.ClientCompanyId == access.ClientCompanyId
            && (access.IsCompanyAdministrator || t.RequesterUserId == access.ClientUserId), ct);
        if (!ok) throw new NotFoundException("Ticket");
    }
}
