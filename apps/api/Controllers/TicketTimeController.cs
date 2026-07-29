using System.ComponentModel.DataAnnotations;
using Desk.Api.Auth;
using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Domain.Authorization;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Controllers;

/// <summary>
/// Staff time management. Lists, logs, edits and deletes PSA time entries for a synced ticket, then
/// recomputes the portal ticket's worked/billable/non-billable aggregates from the PSA (the system
/// of record) so the productivity dashboards stay in sync. A technician action gated by
/// <see cref="Permissions.TicketsLogTime"/> — distinct from the client-portal ticket endpoints.
/// </summary>
[ApiController]
[Route("api/tickets")]
public sealed class TicketTimeController(DeskDbContext db, IConnectorResolver connectors) : ControllerBase
{
    [HttpGet("{id:guid}/time-options")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> TimeOptions(Guid id, CancellationToken ct)
    {
        var ticket = await LoadAsync(id, ct);
        var connector = await connectors.ResolveAsync(ticket.PsaConnectionId, ct);
        var workTypes = await Safe(() => connector.GetWorkTypesAsync(ct));
        var workRoles = await Safe(() => connector.GetWorkRolesAsync(ct));
        return Ok(new
        {
            workTypes = workTypes.Select(o => new { o.Value, o.Label }),
            workRoles = workRoles.Select(o => new { o.Value, o.Label }),
        });
    }

    [HttpGet("{id:guid}/time")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        var ticket = await LoadAsync(id, ct);
        if (string.IsNullOrEmpty(ticket.ExternalTicketId)) return Ok(Array.Empty<object>());
        var connector = await connectors.ResolveAsync(ticket.PsaConnectionId, ct);
        var entries = await connector.GetTimeEntriesAsync(ticket.ExternalTicketId, ct);
        return Ok(entries
            .OrderByDescending(e => e.EntryDate)
            .Select(e => new { externalId = e.ExternalId, hours = e.Hours, billable = e.Billable, entryDate = e.EntryDate, notes = e.Notes, technician = e.TechnicianExternalId }));
    }

    [HttpPost("{id:guid}/time")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> LogTime(Guid id, [FromBody] LogTimeRequest req, CancellationToken ct)
    {
        var (ticket, connector) = await LoadSyncedAsync(id, ct);
        var result = await connector.AddTimeEntryAsync(ticket.ExternalTicketId!,
            new UnifiedTimeEntryCreateRequest(req.Hours,
                Blank(req.WorkType), Blank(req.WorkRole), ParseBillable(req.Billable), req.Notes, MemberIdentifier: null), ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the time entry.");
        return Ok(await RecomputeAsync(ticket, connector, ct));
    }

    [HttpPut("{id:guid}/time/{entryId}")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> Update(Guid id, string entryId, [FromBody] UpdateTimeRequest req, CancellationToken ct)
    {
        var (ticket, connector) = await LoadSyncedAsync(id, ct);
        var result = await connector.UpdateTimeEntryAsync(entryId,
            new UnifiedTimeEntryUpdate(req.Hours, req.Billable is null ? null : ParseBillable(req.Billable), req.Notes), ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the change.");
        return Ok(await RecomputeAsync(ticket, connector, ct));
    }

    [HttpDelete("{id:guid}/time/{entryId}")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> Delete(Guid id, string entryId, CancellationToken ct)
    {
        var (ticket, connector) = await LoadSyncedAsync(id, ct);
        var result = await connector.DeleteTimeEntryAsync(entryId, ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the deletion.");
        return Ok(await RecomputeAsync(ticket, connector, ct));
    }

    // ---- helpers ----

    private async Task<Ticket> LoadAsync(Guid id, CancellationToken ct)
        => await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Ticket");

    private async Task<(Ticket ticket, IServiceManagementConnector connector)> LoadSyncedAsync(Guid id, CancellationToken ct)
    {
        var ticket = await LoadAsync(id, ct);
        if (string.IsNullOrEmpty(ticket.ExternalTicketId))
            throw new ValidationFailedException("This ticket is not yet synced to the PSA, so time cannot be logged.");
        return (ticket, await connectors.ResolveAsync(ticket.PsaConnectionId, ct));
    }

    // Re-read the ticket's entries from the PSA (source of truth) and rewrite the portal aggregates.
    private async Task<object> RecomputeAsync(Ticket ticket, IServiceManagementConnector connector, CancellationToken ct)
    {
        var entries = await connector.GetTimeEntriesAsync(ticket.ExternalTicketId!, ct);
        ticket.TimeWorkedHours = entries.Sum(e => e.Hours);
        ticket.BillableHours = entries.Where(e => e.Billable).Sum(e => e.Hours);
        ticket.NonBillableHours = entries.Where(e => !e.Billable).Sum(e => e.Hours);
        await db.SaveChangesAsync(ct);
        return new
        {
            count = entries.Count,
            timeWorkedHours = ticket.TimeWorkedHours,
            billableHours = ticket.BillableHours,
            nonBillableHours = ticket.NonBillableHours,
        };
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static BillableOption ParseBillable(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "donotbill" or "do not bill" or "nonbillable" => BillableOption.DoNotBill,
        "nocharge" or "no charge" => BillableOption.NoCharge,
        _ => BillableOption.Billable,
    };

    private static async Task<IReadOnlyList<ExternalFieldOption>> Safe(Func<Task<IReadOnlyList<ExternalFieldOption>>> get)
    {
        try { return await get(); }
        catch { return []; }
    }

    public sealed record LogTimeRequest(
        [Range(0.01, 1000, ErrorMessage = "Hours must be between 0.01 and 1000.")] decimal Hours,
        string? Billable,
        [StringLength(2000)] string? Notes,
        string? WorkType,
        string? WorkRole);

    public sealed record UpdateTimeRequest(
        [Range(0.01, 1000)] decimal? Hours,
        string? Billable,
        [StringLength(2000)] string? Notes);
}
