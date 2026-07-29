using System.ComponentModel.DataAnnotations;
using Desk.Api.Auth;
using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Domain.Authorization;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Controllers;

/// <summary>
/// Staff time logging. Posts a time entry to the PSA against a synced ticket, then bumps the portal
/// ticket's worked/billable/non-billable aggregates so the productivity dashboards reflect it. This
/// is a technician action gated by <see cref="Permissions.TicketsLogTime"/> — distinct from the
/// client-portal ticket endpoints, which resolve by client identity.
/// </summary>
[ApiController]
[Route("api/tickets")]
public sealed class TicketTimeController(DeskDbContext db, IConnectorResolver connectors) : ControllerBase
{
    [HttpPost("{id:guid}/time")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> LogTime(Guid id, [FromBody] LogTimeRequest req, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Ticket");
        if (string.IsNullOrEmpty(ticket.ExternalTicketId))
            throw new ValidationFailedException("This ticket is not yet synced to the PSA, so time cannot be logged.");

        var billable = req.Billable?.Trim().ToLowerInvariant() switch
        {
            "donotbill" or "do not bill" or "nonbillable" => BillableOption.DoNotBill,
            "nocharge" or "no charge" => BillableOption.NoCharge,
            _ => BillableOption.Billable,
        };

        var connector = await connectors.ResolveAsync(ticket.PsaConnectionId, ct);
        var result = await connector.AddTimeEntryAsync(ticket.ExternalTicketId,
            new UnifiedTimeEntryCreateRequest(req.Hours, WorkType: null, WorkRole: null, billable, req.Notes, MemberIdentifier: null), ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the time entry.");

        ticket.TimeWorkedHours += req.Hours;
        if (billable == BillableOption.Billable) ticket.BillableHours += req.Hours;
        else ticket.NonBillableHours += req.Hours;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            externalId = result.ExternalId,
            timeWorkedHours = ticket.TimeWorkedHours,
            billableHours = ticket.BillableHours,
            nonBillableHours = ticket.NonBillableHours,
        });
    }

    public sealed record LogTimeRequest(
        [Range(0.01, 1000, ErrorMessage = "Hours must be between 0.01 and 1000.")] decimal Hours,
        string? Billable,
        [StringLength(2000)] string? Notes);
}
