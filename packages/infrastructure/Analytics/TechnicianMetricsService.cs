using Desk.Application.Analytics;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Analytics;

public sealed class TechnicianMetricsService(DeskDbContext db, IProductivityScorer scorer, TimeProvider clock)
    : ITechnicianMetricsService
{
    /// <summary>Lightweight projection of the ticket fields the metrics need.</summary>
    private sealed record Row(
        Guid Id, string? Tech, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, DateTimeOffset? ClosedAt,
        DateTimeOffset? SlaDueAt, decimal Worked, decimal Billable, decimal NonBillable, bool HasNote);

    private async Task<List<Row>> LoadAsync(MetricsFilter f, CancellationToken ct)
    {
        // Date filters and ticket age both run off the PSA's raise date, falling back to the row's
        // own timestamp only where the provider gave none. Using the row timestamp as the primary
        // measured from the day the portal IMPORTED the ticket — so a two-month-old ticket looked
        // hours old, and "average resolution time" quietly reported the rollout, not the service.
        var q = db.Tickets.AsNoTracking().AsQueryable();
        if (f.From is { } from) q = q.Where(t => (t.PsaCreatedAt ?? t.CreatedAt) >= from);
        if (f.To is { } to) q = q.Where(t => (t.PsaCreatedAt ?? t.CreatedAt) <= to);
        if (f.TechnicianExternalId is { } tech) q = q.Where(t => t.AssignedTechnicianExternalId == tech);
        if (f.ClientCompanyId is { } company) q = q.Where(t => t.ClientCompanyId == company);
        if (f.PsaConnectionId is { } conn) q = q.Where(t => t.PsaConnectionId == conn);
        if (f.Priority is { } prio) q = q.Where(t => t.PortalPriority == prio);

        return await q.Select(t => new Row(
            t.Id, t.AssignedTechnicianExternalId, t.PsaCreatedAt ?? t.CreatedAt, t.ResolvedAt, t.ClosedAt, t.SlaDueAt,
            t.TimeWorkedHours, t.BillableHours, t.NonBillableHours,
            t.Notes.Any(n => n.IsPublic))).ToListAsync(ct);
    }

    public async Task<TechnicianMetrics> ForTechnicianAsync(MetricsFilter filter, ProductivityWeights weights, CancellationToken ct = default)
        => Compute(filter.TechnicianExternalId ?? "(all)", await LoadAsync(filter, ct), weights);

    public async Task<IReadOnlyList<TeamComparisonRow>> TeamAsync(MetricsFilter filter, ProductivityWeights weights, CancellationToken ct = default)
    {
        var rows = await LoadAsync(filter, ct);
        return rows
            .Where(r => r.Tech is not null)
            .GroupBy(r => r.Tech!)
            .Select(g =>
            {
                var m = Compute(g.Key, g.ToList(), weights);
                return new TeamComparisonRow(g.Key, m.Resolved, m.SlaCompliancePct, m.Score?.Overall);
            })
            .OrderByDescending(r => r.Score ?? -1)
            .ToList();
    }

    public async Task<IReadOnlyList<TrendPoint>> TrendAsync(MetricsFilter filter, CancellationToken ct = default)
    {
        var rows = await LoadAsync(filter, ct);
        var created = rows.GroupBy(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());
        var resolved = rows.Where(r => r.ResolvedAt is not null)
            .GroupBy(r => DateOnly.FromDateTime(r.ResolvedAt!.Value.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());

        return created.Keys.Union(resolved.Keys)
            .OrderBy(d => d)
            .Select(d => new TrendPoint(d, created.GetValueOrDefault(d), resolved.GetValueOrDefault(d)))
            .ToList();
    }

    private TechnicianMetrics Compute(string tech, List<Row> rows, ProductivityWeights weights)
    {
        var now = clock.GetUtcNow();
        var assigned = rows.Count;
        var resolvedRows = rows.Where(r => r.ResolvedAt is not null).ToList();
        var resolved = resolvedRows.Count;
        var open = rows.Count(r => r.ResolvedAt is null && r.ClosedAt is null);
        var overdue = rows.Count(r => r.ResolvedAt is null && r.SlaDueAt is { } d && d < now);

        var slaEligible = resolvedRows.Count(r => r.SlaDueAt is not null);
        var withinSla = resolvedRows.Count(r => r.SlaDueAt is { } d && r.ResolvedAt <= d);
        var slaPct = slaEligible > 0 ? Math.Round(100.0 * withinSla / slaEligible, 1) : 0;

        var avgResolutionHours = resolved > 0
            ? Math.Round(resolvedRows.Average(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalHours), 1)
            : 0;

        // Proxy component scores from measurable ticket data. CSAT / first-response / reopen are not
        // tracked yet and remain unmeasured (excluded from the weighted score via renormalization).
        var components = new ProductivityComponents
        {
            SlaCompliance = slaEligible > 0 ? slaPct : null,
            ResolutionRate = assigned > 0 ? Math.Round(100.0 * resolved / assigned, 1) : null,
            WorklogQuality = resolved > 0 ? Math.Round(100.0 * resolvedRows.Count(r => r.Worked > 0) / resolved, 1) : null,
            DocumentationQuality = resolved > 0 ? Math.Round(100.0 * resolvedRows.Count(r => r.HasNote) / resolved, 1) : null,
        };

        return new TechnicianMetrics
        {
            TechnicianExternalId = tech,
            Assigned = assigned,
            Resolved = resolved,
            Open = open,
            Overdue = overdue,
            WithinSla = withinSla,
            SlaEligible = slaEligible,
            SlaCompliancePct = slaPct,
            AvgResolutionHours = avgResolutionHours,
            TimeWorkedHours = rows.Sum(r => r.Worked),
            BillableHours = rows.Sum(r => r.Billable),
            NonBillableHours = rows.Sum(r => r.NonBillable),
            Components = components,
            Score = scorer.Calculate(components, weights),
        };
    }
}
