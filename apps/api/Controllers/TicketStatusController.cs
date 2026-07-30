using Desk.Api.Auth;
using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Application.Mapping;
using Desk.Domain.Authorization;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Controllers;

/// <summary>
/// Staff status changes (incl. closing a ticket). Maps the chosen portal-neutral status to the PSA's
/// real value via the mapping rules, pushes it to the provider (PSA = system of record), then updates
/// the portal projection. Gated by <see cref="Permissions.TicketsUpdate"/>.
/// </summary>
[ApiController]
[Route("api/tickets")]
public sealed class TicketStatusController(
    DeskDbContext db,
    IConnectorResolver connectors,
    IMappingEngine mapping,
    IConnectionAdminService admin) : ControllerBase
{
    [HttpPost("{id:guid}/status")]
    [RequirePermission(Permissions.TicketsUpdate)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetStatusRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Status))
            throw new ValidationFailedException("A status is required.");

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Ticket");
        if (string.IsNullOrEmpty(ticket.ExternalTicketId))
            throw new ValidationFailedException("This ticket is not yet synced to the PSA.");

        var portalStatus = req.Status.Trim();
        var rules = await db.FieldMappings.AsNoTracking()
            .Where(m => m.Provider == ticket.Provider && m.IsActive).ToListAsync(ct);
        var ctx = new MappingContext { Provider = ticket.Provider, PsaConnectionId = ticket.PsaConnectionId, QueueOrBoardKey = ticket.QueueOrBoard };

        // Portal → PSA value (name). Fall back to the raw portal value when no rule matches.
        var mappedName = mapping.MapToProvider(rules, ctx, "status", portalStatus).Value ?? portalStatus;

        // Prefer the PSA status id (more reliable than name on board-scoped statuses): resolve the
        // discovered option whose label matches the mapped name.
        var fields = await admin.GetFieldsAsync(ticket.PsaConnectionId, ct);
        var external = fields.Statuses.FirstOrDefault(s => string.Equals(s.Label, mappedName, StringComparison.OrdinalIgnoreCase))?.Value ?? mappedName;

        var connector = await connectors.ResolveAsync(ticket.PsaConnectionId, ct);
        var result = await connector.UpdateTicketAsync(ticket.ExternalTicketId,
            new UnifiedTicketUpdate { Status = external, IdempotencyKey = Guid.NewGuid().ToString("N") }, ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the status change.");

        ticket.PortalStatus = portalStatus;
        ticket.PsaStatus = mappedName;
        await db.SaveChangesAsync(ct);
        return Ok(new { portalStatus = ticket.PortalStatus });
    }

    public sealed record SetStatusRequest(string Status);
}
