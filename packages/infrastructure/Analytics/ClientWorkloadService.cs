using Desk.Application.Analytics;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Analytics;

/// <summary>
/// Where the desk's capacity goes, by client.
///
/// Every figure comes from what the PSA reported and the portal stored — no estimate, no
/// apportionment, no filling of gaps. Where a figure cannot be computed for a ticket, that ticket
/// is excluded from that figure and counted in the report's coverage fields instead, so the surface
/// can say what it measured rather than quietly averaging over whatever happened to be present.
/// </summary>
public sealed class ClientWorkloadService(DeskDbContext db) : IClientWorkloadService
{
    public async Task<ClientWorkloadReport> ForClientsAsync(MetricsFilter filter, CancellationToken ct = default)
    {
        var q = db.Tickets.AsNoTracking().AsQueryable();
        // Ticket age and every date filter run off the PSA's raise date, falling back to the row
        // timestamp only where the provider gave none — the row timestamp is when the portal
        // imported the ticket, which is a fact about the rollout, not about the work.
        if (filter.From is { } from) q = q.Where(t => (t.PsaCreatedAt ?? t.CreatedAt) >= from);
        if (filter.To is { } to) q = q.Where(t => (t.PsaCreatedAt ?? t.CreatedAt) <= to);
        if (filter.ClientCompanyId is { } company) q = q.Where(t => t.ClientCompanyId == company);
        if (filter.PsaConnectionId is { } conn) q = q.Where(t => t.PsaConnectionId == conn);

        var rows = await q
            .Select(t => new
            {
                t.ClientCompanyId,
                t.PsaCreatedAt,
                RowCreatedAt = t.CreatedAt,
                t.ClosedAt,
                t.ResolvedAt,
                t.SlaDueAt,
                t.AssignedTechnicianExternalId,
                t.TimeWorkedHours,
                t.BillableHours,
            })
            .ToListAsync(ct);

        var names = await db.ClientCompanies.AsNoTracking()
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        // Who actually worked the ticket, from the TIME ENTRIES rather than the assignee: hours
        // belong to whoever logged them, and a ticket is commonly worked by someone other than the
        // person it is assigned to — or by several people.
        var workers = await db.TicketTimeEntries.AsNoTracking()
            .Where(e => e.TechnicianExternalId != null)
            .Join(db.Tickets.AsNoTracking(), e => e.TicketId, t => t.Id,
                (e, t) => new { t.ClientCompanyId, e.TechnicianExternalId })
            .Distinct()
            .ToListAsync(ct);
        var workersByClient = workers
            .GroupBy(w => w.ClientCompanyId)
            .ToDictionary(g => g.Key, g => g.Select(w => w.TechnicianExternalId!).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var clients = rows
            .GroupBy(t => t.ClientCompanyId)
            .Select(g =>
            {
                // Resolution time needs BOTH ends. A ticket missing either is excluded and
                // reported, never treated as instant.
                var resolved = g
                    .Where(t => t.ClosedAt is not null && (t.PsaCreatedAt ?? t.RowCreatedAt) is var raised && t.ClosedAt > raised)
                    .Select(t => (t.ClosedAt!.Value - (t.PsaCreatedAt ?? t.RowCreatedAt)).TotalHours)
                    .ToList();

                var slaEligible = g.Where(t => t.SlaDueAt is not null && t.ClosedAt is not null).ToList();
                var withinSla = slaEligible.Count(t => t.ClosedAt <= t.SlaDueAt);

                // Assignees and time-loggers together: either alone under-counts who touched it.
                var involved = g
                    .Where(t => !string.IsNullOrEmpty(t.AssignedTechnicianExternalId))
                    .Select(t => t.AssignedTechnicianExternalId!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (workersByClient.TryGetValue(g.Key, out var loggers)) involved.UnionWith(loggers);

                return new ClientWorkloadRow(
                    g.Key,
                    names.GetValueOrDefault(g.Key, "Unknown client"),
                    g.Count(),
                    g.Count(t => t.ClosedAt is null),
                    g.Count(t => t.ClosedAt is not null),
                    g.Sum(t => t.TimeWorkedHours),
                    g.Sum(t => t.BillableHours),
                    involved.Count,
                    resolved.Count > 0 ? Math.Round(resolved.Average(), 1) : null,
                    resolved.Count,
                    slaEligible.Count > 0 ? Math.Round(withinSla * 100.0 / slaEligible.Count, 1) : null,
                    slaEligible.Count);
            })
            // Hours first: the question this answers is where capacity goes, and hours are capacity.
            .OrderByDescending(c => c.HoursWorked)
            .ThenByDescending(c => c.TotalTickets)
            .ToList();

        var windows = await db.PsaConnections.AsNoTracking()
            .Select(c => new ImportWindowNote(
                c.Name, c.ImportClosedTickets, c.FilterActiveWithinDays,
                db.Tickets.Count(t => t.PsaConnectionId == c.Id)))
            .ToListAsync(ct);

        return new ClientWorkloadReport(
            clients,
            rows.Count(t => t.PsaCreatedAt is null),
            rows.Count(t => t.ClosedAt is null),
            windows);
    }
}
