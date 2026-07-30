using Desk.Application.Tickets;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Tickets;

/// <summary>
/// Client-portal reads. All queries are constrained to the caller's company, and to their own
/// tickets when they are not a company administrator. Detail exposes only the public conversation;
/// internal PSA notes are never persisted to the portal, so they cannot leak here.
/// </summary>
public sealed class TicketReadService(DeskDbContext db) : ITicketReadService
{
    private IQueryable<Ticket> Visible(ClientAccess access) =>
        db.Tickets.Where(t =>
            t.ClientCompanyId == access.ClientCompanyId
            && (access.IsCompanyAdministrator || t.RequesterUserId == access.ClientUserId));

    public async Task<IReadOnlyList<TicketListItem>> ListAsync(ClientAccess access, CancellationToken ct = default)
        => await Visible(access)
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            // Company + connection names let users tell tickets apart when an MSP runs multiple
            // PSA connections (even several tenants of the same provider).
            .Select(t => new TicketListItem(
                t.Id, t.ExternalTicketId, t.Provider, t.Title, t.PortalStatus, t.PortalPriority,
                t.QueueOrBoard, t.CreatedAt, t.LastSyncedAt,
                db.ClientCompanies.Where(c => c.Id == t.ClientCompanyId).Select(c => c.Name).FirstOrDefault(),
                db.PsaConnections.Where(p => p.Id == t.PsaConnectionId).Select(p => p.Name).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<TicketDetailDto?> GetDetailAsync(ClientAccess access, Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await Visible(access)
            .AsNoTracking()
            .Include(t => t.Notes)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return null; // not found OR not permitted — indistinguishable to the client

        var customerName = await db.ClientCompanies
            .AsNoTracking()
            .Where(c => c.Id == ticket.ClientCompanyId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);
        var connectionName = await db.PsaConnections
            .AsNoTracking()
            .Where(p => p.Id == ticket.PsaConnectionId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);

        return new TicketDetailDto(
            ticket.Id, ticket.ExternalTicketId, ticket.Provider, ticket.Title, ticket.Description,
            ticket.PortalStatus, ticket.PortalPriority, ticket.PortalCategory, ticket.QueueOrBoard,
            ticket.CreatedAt, ticket.ResolvedAt,
            Conversation: ticket.Notes
                .Where(n => n.IsPublic) // defensive: only public notes ever reach a client
                .OrderBy(n => n.NoteCreatedAt)
                .Select(n => new TicketNoteDto(n.Id, n.AuthorName, n.AuthoredByClient, n.Body, n.NoteCreatedAt))
                .ToList(),
            Attachments: ticket.Attachments
                .OrderBy(a => a.UploadedAt)
                .Select(a => new AttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.ScanStatus, a.UploadedAt))
                .ToList(),
            CustomerName: customerName,
            UpdatedAt: ticket.UpdatedAt,
            ConnectionName: connectionName);
    }

    public async Task<IReadOnlyList<NotificationDto>> RecentActivityAsync(ClientAccess access, int take = 10, CancellationToken ct = default)
        => await Visible(access)
            .AsNoTracking()
            .OrderByDescending(t => t.LastSyncedAt ?? t.CreatedAt)
            .Take(take)
            .Select(t => new NotificationDto(
                t.Id, t.Title, "ticket-updated",
                "Status: " + t.PortalStatus, t.LastSyncedAt ?? t.CreatedAt))
            .ToListAsync(ct);
}
