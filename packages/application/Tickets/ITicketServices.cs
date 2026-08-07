namespace Desk.Application.Tickets;

/// <summary>Resolves the client identity (company + user) for an authenticated subject, if any.</summary>
public interface IClientAccessResolver
{
    Task<ClientAccess?> ResolveAsync(string idpSubject, CancellationToken ct = default);
}

/// <summary>
/// Read side of the client portal. Every method is constrained to the caller's company (and, for
/// non-admins, their own tickets). Detail returns only the public conversation.
/// </summary>
public interface ITicketReadService
{
    Task<IReadOnlyList<TicketListItem>> ListAsync(ClientAccess access, CancellationToken ct = default);
    Task<TicketDetailDto?> GetDetailAsync(ClientAccess access, Guid ticketId, CancellationToken ct = default);

    /// <summary>Every ticket in the tenant, for staff holding TicketsViewAll.</summary>
    Task<IReadOnlyList<TicketListItem>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Any ticket in the tenant, for staff holding TicketsViewAll.</summary>
    Task<TicketDetailDto?> GetDetailForStaffAsync(Guid ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> RecentActivityAsync(ClientAccess access, int take = 10, CancellationToken ct = default);
}

/// <summary>
/// Write side. Creating a ticket or adding a comment goes to the PSA (system of record) first, then
/// the portal projection is updated and a portal-origin sync event is recorded so the inbound sync
/// recognises and skips its own echo.
/// </summary>
public interface ITicketCommandService
{
    Task<CreateTicketResultDto> CreateAsync(ClientAccess access, CreateTicketInput input, CancellationToken ct = default);
    Task<TicketNoteDto> AddCommentAsync(ClientAccess access, Guid ticketId, string body, CancellationToken ct = default);

    /// <summary>A technician reply on any tenant ticket, attributed by name. Gate on TicketsViewAll.</summary>
    Task<TicketNoteDto> AddStaffCommentAsync(string authorName, Guid ticketId, string body, CancellationToken ct = default);
}
