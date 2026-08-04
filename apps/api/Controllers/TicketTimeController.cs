using System.ComponentModel.DataAnnotations;
using Desk.Api.Auth;
using Desk.Application.Admin;
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
public sealed class TicketTimeController(DeskDbContext db, IConnectorResolver connectors, IConnectionAdminService admin) : ControllerBase
{
    [HttpGet("{id:guid}/time-options")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> TimeOptions(Guid id, CancellationToken ct)
    {
        var ticket = await LoadAsync(id, ct);
        // Read from the cached field set (discovered when the connection was configured).
        var fields = await admin.GetFieldsAsync(ticket.PsaConnectionId, ct);
        return Ok(new
        {
            workTypes = fields.WorkTypes.Select(o => new { o.Value, o.Label }),
            workRoles = fields.WorkRoles.Select(o => new { o.Value, o.Label }),
        });
    }

    /// <summary>
    /// The ticket's time, reconciled from two places. The PSA owns the hours, so its entries are the
    /// spine of the list; the portal's own rows supply what the PSA cannot know — which system the
    /// entry was logged in — and carry the entries that never made it, which would otherwise be
    /// invisible to everyone.
    /// </summary>
    [HttpGet("{id:guid}/time")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        var ticket = await LoadAsync(id, ct);
        var local = await db.TicketTimeEntries.AsNoTracking()
            .Where(t => t.TicketId == id)
            .ToListAsync(ct);

        if (string.IsNullOrEmpty(ticket.ExternalTicketId))
            return Ok(local.OrderByDescending(l => l.EntryDate).Select(LocalRow));

        var connector = await connectors.ResolveAsync(ticket.PsaConnectionId, ct);
        var entries = await connector.GetTimeEntriesAsync(ticket.ExternalTicketId, ct);

        var portalByExternalId = local
            .Where(l => l.ExternalEntryId is not null)
            .ToDictionary(l => l.ExternalEntryId!, l => l);

        var rows = entries.Select(e =>
        {
            portalByExternalId.TryGetValue(e.ExternalId, out var origin);
            return new TimeRow(
                e.ExternalId, e.Hours, e.Billable, e.BillableOption.ToString(), e.EntryDate, e.Notes,
                e.TechnicianExternalId, e.TechnicianName, e.WorkType,
                origin is null ? nameof(TimeEntrySource.Provider) : nameof(TimeEntrySource.Portal),
                nameof(TimeEntrySyncStatus.Synced), null);
        }).ToList();

        // Anything the PSA rejected or has not accepted yet: it exists only here.
        rows.AddRange(local
            .Where(l => l.SyncStatus != TimeEntrySyncStatus.Synced)
            .Select(LocalRow));

        return Ok(rows.OrderByDescending(r => r.EntryDate));
    }

    private static TimeRow LocalRow(TicketTimeEntry l) => new(
        l.ExternalEntryId ?? l.Id.ToString(), l.Hours, l.Billable,
        l.Billable ? nameof(BillableOption.Billable) : nameof(BillableOption.DoNotBill),
        l.EntryDate, l.Notes, l.TechnicianExternalId, l.TechnicianName, l.WorkTypeLabel,
        l.Source.ToString(), l.SyncStatus.ToString(), l.SyncError);

    /// <summary>One row of the time panel, whichever side it came from.</summary>
    public sealed record TimeRow(
        string ExternalId, decimal Hours, bool Billable, string BillableOption, DateTimeOffset EntryDate,
        string? Notes, string? Technician, string? TechnicianName, string? WorkType,
        string Source, string SyncStatus, string? SyncError);

    [HttpPost("{id:guid}/time")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> LogTime(Guid id, [FromBody] LogTimeRequest req, CancellationToken ct)
    {
        var (ticket, connector) = await LoadSyncedAsync(id, ct);
        var billable = ParseBillable(req.Billable);

        // Written before the push, not after: a rejected entry used to disappear with the 400, taking
        // the technician's logged work with it and leaving nothing to retry from.
        var record = new TicketTimeEntry
        {
            MspOrganizationId = ticket.MspOrganizationId,
            TicketId = ticket.Id,
            Hours = req.Hours,
            Billable = billable == BillableOption.Billable,
            Notes = req.Notes,
            WorkTypeId = Blank(req.WorkType),
            WorkTypeLabel = await WorkTypeLabelAsync(ticket, Blank(req.WorkType), ct),
            WorkRoleId = Blank(req.WorkRole),
            Source = TimeEntrySource.Portal,
            SyncStatus = TimeEntrySyncStatus.Pending,
            EntryDate = DateTimeOffset.UtcNow,
        };
        db.TicketTimeEntries.Add(record);
        await db.SaveChangesAsync(ct);

        if (!await PushAsync(record, ticket, connector, ct))
            throw new ValidationFailedException(record.SyncError ?? "The PSA rejected the time entry.");

        return Ok(await RecomputeAsync(ticket, connector, ct));
    }

    /// <summary>
    /// Re-sends an entry the PSA previously rejected. Nothing about the entry is re-entered: the
    /// original hours, work type and notes are pushed again, so fixing the connection is all it
    /// takes to recover work that would otherwise have to be typed in a second time.
    /// </summary>
    [HttpPost("{id:guid}/time/{entryId:guid}/retry")]
    [RequirePermission(Permissions.TicketsLogTime)]
    public async Task<IActionResult> Retry(Guid id, Guid entryId, CancellationToken ct)
    {
        var (ticket, connector) = await LoadSyncedAsync(id, ct);
        var record = await db.TicketTimeEntries.FirstOrDefaultAsync(t => t.Id == entryId && t.TicketId == id, ct)
            ?? throw new NotFoundException("Time entry");
        if (record.SyncStatus == TimeEntrySyncStatus.Synced)
            throw new ValidationFailedException("This entry is already recorded in the PSA.");

        if (!await PushAsync(record, ticket, connector, ct))
            throw new ValidationFailedException(record.SyncError ?? "The PSA rejected the time entry again.");

        return Ok(await RecomputeAsync(ticket, connector, ct));
    }

    /// <summary>Pushes a portal record to the PSA and stamps the outcome on it either way.</summary>
    private async Task<bool> PushAsync(TicketTimeEntry record, Ticket ticket, IServiceManagementConnector connector, CancellationToken ct)
    {
        var result = await connector.AddTimeEntryAsync(ticket.ExternalTicketId!,
            new UnifiedTimeEntryCreateRequest(record.Hours, record.WorkTypeId, record.WorkRoleId,
                record.Billable ? BillableOption.Billable : BillableOption.DoNotBill,
                record.Notes, MemberIdentifier: null), ct);

        if (!result.Success)
        {
            record.SyncStatus = TimeEntrySyncStatus.Failed;
            record.SyncError = result.Error;
            await db.SaveChangesAsync(ct);
            return false;
        }

        record.ExternalEntryId = result.ExternalId;
        record.SyncStatus = TimeEntrySyncStatus.Synced;
        record.SyncError = null;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Resolves a work-type id to its label once, so the row stays readable later.</summary>
    private async Task<string?> WorkTypeLabelAsync(Ticket ticket, string? workTypeId, CancellationToken ct)
    {
        if (workTypeId is null) return null;
        try
        {
            var fields = await admin.GetFieldsAsync(ticket.PsaConnectionId, ct);
            return fields.WorkTypes.FirstOrDefault(o => o.Value == workTypeId)?.Label;
        }
        catch (Exception) { return null; } // a label is cosmetic; never fail a time log over it
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

        // A rejected entry has no provider counterpart — its id is the portal's own. Asking the PSA
        // to delete it would fail, leaving the row stuck on screen with no way to clear it.
        if (Guid.TryParse(entryId, out var localId))
        {
            var unsynced = await db.TicketTimeEntries
                .FirstOrDefaultAsync(t => t.Id == localId && t.TicketId == id && t.ExternalEntryId == null, ct);
            if (unsynced is not null)
            {
                db.TicketTimeEntries.Remove(unsynced);
                await db.SaveChangesAsync(ct);
                return Ok(await RecomputeAsync(ticket, connector, ct));
            }
        }

        var result = await connector.DeleteTimeEntryAsync(entryId, ct);
        if (!result.Success)
            throw new ValidationFailedException(result.Error ?? "The PSA rejected the deletion.");

        // Drop the portal's mirror too, or the entry reappears as an unsynced ghost.
        var local = await db.TicketTimeEntries.Where(t => t.TicketId == id && t.ExternalEntryId == entryId).ToListAsync(ct);
        if (local.Count > 0) { db.TicketTimeEntries.RemoveRange(local); await db.SaveChangesAsync(ct); }

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
