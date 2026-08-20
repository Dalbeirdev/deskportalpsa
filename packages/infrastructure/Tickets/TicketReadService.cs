using Desk.Application.Abstractions;
using Desk.Application.Tickets;
using Desk.Domain.Authorization;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Tickets;

/// <summary>
/// Client-portal reads. All queries are constrained to the caller's company, and to their own
/// tickets when they are not a company administrator. Detail exposes only the public conversation;
/// internal PSA notes are never persisted to the portal, so they cannot leak here.
///
/// The staff-side methods below route through <see cref="ITicketScopeQuery"/> for the caller's
/// effective TicketsViewAll scope — the tenant filter alone bounds them to the organization, but
/// says nothing about which tickets WITHIN it the caller may see.
/// </summary>
public sealed class TicketReadService(DeskDbContext db, ITicketScopeQuery scopeQuery, ICurrentUser user) : ITicketReadService
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

    /// <summary>
    /// Every ticket the tenant holds, across all connections and companies — the STAFF list. The
    /// client-scoped list above shows one company's tickets; an MSP admin looking at that saw only
    /// whichever company their login happened to be bound to, and concluded a whole PSA was missing.
    /// Callers gate this on TicketsViewAll; the tenant filter on the DbContext bounds it to the org.
    /// </summary>
    public async Task<IReadOnlyList<TicketListItem>> ListAllAsync(CancellationToken ct = default)
    {
        var visible = await StaffVisibleAsync(ct);
        return await visible
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TicketListItem(
                t.Id, t.ExternalTicketId, t.Provider, t.Title, t.PortalStatus, t.PortalPriority,
                t.QueueOrBoard, t.CreatedAt, t.LastSyncedAt,
                db.ClientCompanies.Where(c => c.Id == t.ClientCompanyId).Select(c => c.Name).FirstOrDefault(),
                db.PsaConnections.Where(p => p.Id == t.PsaConnectionId).Select(p => p.Name).FirstOrDefault()))
            .ToListAsync(ct);
    }

    /// <summary>Staff callers always resolve against TicketsViewAll — it is the only permission that
    /// reaches these two methods (the controller branches to the client-scoped path otherwise) — so
    /// a caller with no linked AppUser row has nothing to resolve and sees nothing, not everything.</summary>
    private async Task<IQueryable<Ticket>> StaffVisibleAsync(CancellationToken ct)
        => user.UserId is { } uid
            ? await scopeQuery.VisibleAsync(db.Tickets, uid, Permissions.TicketsViewAll, ct)
            : db.Tickets.Where(_ => false);

    public Task<TicketDetailDto?> GetDetailAsync(ClientAccess access, Guid ticketId, CancellationToken ct = default)
        => DetailAsync(t => Visible(access), ticketId, includeInternal: false, ct);

    /// <summary>Staff detail, narrowed to the caller's effective TicketsViewAll scope — NOT every
    /// ticket in the tenant, despite the name; the coarse permission check that used to gate the
    /// whole method said nothing about which rows within it are actually theirs to see.</summary>
    public async Task<TicketDetailDto?> GetDetailForStaffAsync(Guid ticketId, CancellationToken ct = default)
    {
        var visible = await StaffVisibleAsync(ct);
        // Staff see the WHOLE thread, internal notes included — that is what distinguishes the
        // technician view from the client one, and hiding them here is how CW-side internal notes
        // silently vanished from the portal.
        return await DetailAsync(_ => visible, ticketId, includeInternal: true, ct);
    }

    private async Task<TicketDetailDto?> DetailAsync(
        Func<object?, IQueryable<Ticket>> scope, Guid ticketId, bool includeInternal, CancellationToken ct)
    {
        var ticket = await scope(null)
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
        var connection = await db.PsaConnections
            .AsNoTracking()
            .Where(p => p.Id == ticket.PsaConnectionId)
            .Select(p => new { p.Name, p.ApiEndpoint })
            .FirstOrDefaultAsync(ct);
        var connectionName = connection?.Name;
        // Built from the endpoint we already have — no credentials, so no vault call to render.
        var externalUrl = PsaTicketLink.For(ticket.Provider, connection?.ApiEndpoint, ticket.ExternalTicketId);

        // Ticket service instructions the client configured: the account-specific override if set,
        // otherwise the organization-wide default. Surfaced so technicians see them on the ticket.
        var instructions = await db.TicketInstructions
            .AsNoTracking()
            .Where(i => i.ClientCompanyId == ticket.ClientCompanyId || i.ClientCompanyId == null)
            .ToListAsync(ct);
        var serviceInstructions = instructions.FirstOrDefault(i => i.ClientCompanyId == ticket.ClientCompanyId)?.Body;
        if (string.IsNullOrWhiteSpace(serviceInstructions))
            serviceInstructions = instructions.FirstOrDefault(i => i.ClientCompanyId == null)?.Body;
        serviceInstructions = string.IsNullOrWhiteSpace(serviceInstructions) ? null : serviceInstructions;

        return new TicketDetailDto(
            ticket.Id, ticket.ExternalTicketId, ticket.Provider, ticket.Title, ticket.Description,
            ticket.PortalStatus, ticket.PortalPriority, ticket.PortalCategory, ticket.QueueOrBoard,
            ticket.CreatedAt, ticket.ResolvedAt,
            Conversation: ticket.Notes
                .Where(n => includeInternal || n.IsPublic) // clients NEVER receive internal notes
                .OrderBy(n => n.NoteCreatedAt)
                .Select(n => new TicketNoteDto(n.Id, n.AuthorName, n.AuthoredByClient, n.Body, n.NoteCreatedAt, n.IsPublic))
                .ToList(),
            Attachments: ticket.Attachments
                .OrderBy(a => a.UploadedAt)
                .Select(a => new AttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.SizeBytes, a.ScanStatus, a.UploadedAt)
                    { AuthorName = a.AuthorName, FromProvider = a.ImportedFromProvider, TicketNoteId = a.TicketNoteId })
                .ToList(),
            CustomerName: customerName,
            UpdatedAt: ticket.UpdatedAt,
            ConnectionName: connectionName,
            ServiceInstructions: serviceInstructions,
            AssignedTechnicianExternalId: ticket.AssignedTechnicianExternalId,
            AssignedTechnicianName: ticket.AssignedTechnicianName,
            ExternalTicketUrl: externalUrl);
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
