namespace Desk.Application.Analytics;

/// <summary>
/// Aggregates ticket data into technician/manager dashboard metrics. All queries are tenant-scoped;
/// callers additionally constrain by the supplied filter. Component scores are derived from what the
/// ticket data actually supports today (SLA, resolution, worklog + documentation proxies); untracked
/// signals (CSAT, first-response, reopen) are left unmeasured and excluded from the score.
/// </summary>
public interface ITechnicianMetricsService
{
    Task<TechnicianMetrics> ForTechnicianAsync(MetricsFilter filter, ProductivityWeights weights, CancellationToken ct = default);
    Task<IReadOnlyList<TeamComparisonRow>> TeamAsync(MetricsFilter filter, ProductivityWeights weights, CancellationToken ct = default);
    Task<IReadOnlyList<TrendPoint>> TrendAsync(MetricsFilter filter, CancellationToken ct = default);
}
