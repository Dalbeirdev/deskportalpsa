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
        var orgId = await AuthorizeTicketAsync(ticketId, ct);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var dto = await attachments.UploadAsync(new UploadAttachmentInput(
            ticketId, orgId, file.FileName, file.ContentType, ms.ToArray(), noteId), ct);

        return Ok(dto);
    }

    [Authorize]
    [HttpGet("tickets/{ticketId:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadUrl(Guid ticketId, Guid attachmentId, CancellationToken ct)
    {
        await AuthorizeTicketAsync(ticketId, ct);

        // The access check above proves the caller may read THIS ticket; it says nothing about the
        // attachment. Without binding the two, a caller could pass a ticket they can see together
        // with an attachment id from a ticket they cannot — including another company's — and the
        // download would be issued for it.
        var belongs = await db.TicketAttachments.AnyAsync(a => a.Id == attachmentId && a.TicketId == ticketId, ct);
        if (!belongs) throw new NotFoundException("Attachment");

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

    /// <summary>
    /// A client acts on their own company's tickets; staff with view-all act on any ticket in the
    /// tenant — the same dual path the comments endpoint uses, because attaching a file to a reply
    /// is part of replying. (This controller used to be client-only, which made every attachment
    /// upload and download from the staff dashboard fail 403 while the reply itself succeeded.)
    /// Client-scoped resolution is tried FIRST so the dual dev identity still acts as the client on
    /// its own company's tickets. Returns the organization that owns the ticket.
    /// </summary>
    private async Task<Guid> AuthorizeTicketAsync(Guid ticketId, CancellationToken ct)
    {
        var access = await accessResolver.ResolveAsync(user.Subject ?? "", ct);
        if (access is not null)
        {
            var ok = await db.Tickets.AnyAsync(t =>
                t.Id == ticketId
                && t.ClientCompanyId == access.ClientCompanyId
                && (access.IsCompanyAdministrator || t.RequesterUserId == access.ClientUserId), ct);
            if (ok) return access.MspOrganizationId;
            // Not their company's ticket — fall through to the staff path if they can hold one.
        }

        if (!user.HasPermission(Permissions.TicketsViewAll))
            throw access is null
                ? new ForbiddenException("This endpoint is for client portal users.")
                : new NotFoundException("Ticket");

        // Staff path: db.Tickets is tenant-scoped, so this cannot cross into another organization.
        var orgId = await db.Tickets.Where(t => t.Id == ticketId)
            .Select(t => (Guid?)t.MspOrganizationId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Ticket");
        return orgId;
    }
}
