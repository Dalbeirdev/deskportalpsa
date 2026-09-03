using System.Globalization;
using System.Text;
using Desk.Application.Abstractions;
using Desk.Application.Analytics;
using Desk.Application.Common;
using Desk.Domain.Authorization;
using Desk.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Api.Controllers;

/// <summary>
/// Staff productivity dashboards (technician + manager). Team-wide views require the team
/// productivity permission; export mirrors the team query. Every response carries the operational-
/// indicator disclaimer so it travels with the numbers.
/// </summary>
// Class-level [Authorize] as a floor: every action here also carries [RequirePermission],
// but that is opt-in per action — an action added later without one would otherwise be
// reachable anonymously. This makes authentication the default and the omission harmless.
[Authorize]
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(
    ITechnicianMetricsService metrics, IClientWorkloadService clients,
    IPortalCoverageService coverage, ICurrentUser user) : ControllerBase
{
    [HttpGet("technician")]
    [RequirePermission(Permissions.ProductivityViewOwn)]
    public async Task<IActionResult> Technician([FromQuery] DashboardQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter();

        // The technician filter arrives from the query string, so on its own this endpoint let
        // anyone holding the own-productivity permission read a NAMED colleague's numbers by
        // passing their id — or the whole organization's by passing nothing at all, since an
        // absent filter means "no restriction" downstream. Callers without the team permission are
        // therefore pinned to themselves regardless of what they asked for.
        if (!user.HasPermission(Permissions.ProductivityViewTeam))
        {
            var self = user.TechnicianExternalId;
            if (string.IsNullOrEmpty(self))
                throw new ForbiddenException(
                    "Your account is not linked to a technician in the PSA, so it has no own-productivity figures to show.");
            filter = filter with { TechnicianExternalId = self };
        }

        var m = await metrics.ForTechnicianAsync(filter, q.ToWeights(), ct);
        return Ok(new { metrics = m, disclaimer = ProductivityScore.Disclaimer });
    }

    /// <summary>
    /// Where the desk's capacity goes, by client. Gated on the TEAM permission: this is
    /// organization-wide commercial information, not someone's own figures.
    /// </summary>
    [HttpGet("clients")]
    [RequirePermission(Permissions.ProductivityViewTeam)]
    public async Task<IActionResult> Clients([FromQuery] DashboardQuery q, CancellationToken ct)
        => Ok(await clients.ForClientsAsync(q.ToFilter(), ct));

    /// <summary>
    /// How much of the work the PSA recorded is visible in this portal. Team-gated: it names
    /// individuals, and it is an operational measure of rollout rather than of people.
    /// </summary>
    [HttpGet("coverage")]
    [RequirePermission(Permissions.ProductivityViewTeam)]
    public async Task<IActionResult> Coverage([FromQuery] DashboardQuery q, CancellationToken ct)
        => Ok(await coverage.CoverageAsync(q.ToFilter(), ct));

    [HttpGet("team")]
    [RequirePermission(Permissions.ProductivityViewTeam)]
    public async Task<IActionResult> Team([FromQuery] DashboardQuery q, CancellationToken ct)
    {
        var rows = await metrics.TeamAsync(q.ToFilter(), q.ToWeights(), ct);
        return Ok(new { team = rows, disclaimer = ProductivityScore.Disclaimer });
    }

    [HttpGet("trend")]
    [RequirePermission(Permissions.ReportsView)]
    public async Task<IActionResult> Trend([FromQuery] DashboardQuery q, CancellationToken ct)
        => Ok(await metrics.TrendAsync(q.ToFilter(), ct));

    [HttpGet("team/export")]
    [RequirePermission(Permissions.ProductivityViewTeam)]
    public async Task<IActionResult> ExportTeam([FromQuery] DashboardQuery q, CancellationToken ct)
    {
        var rows = await metrics.TeamAsync(q.ToFilter(), q.ToWeights(), ct);
        var sb = new StringBuilder();
        sb.AppendLine("# " + ProductivityScore.Disclaimer);
        sb.AppendLine("TechnicianExternalId,Resolved,SlaCompliancePct,ProductivityScore");
        foreach (var r in rows)
            sb.AppendLine(string.Join(',',
                Csv(r.TechnicianExternalId),
                r.Resolved.ToString(CultureInfo.InvariantCulture),
                r.SlaCompliancePct.ToString(CultureInfo.InvariantCulture),
                (r.Score?.ToString(CultureInfo.InvariantCulture) ?? "")));

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "team-productivity.csv");
    }

    // Escapes a CSV field so a technician id containing a comma or quote can't break the columns.
    private static string Csv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    public sealed record DashboardQuery
    {
        public DateTimeOffset? From { get; init; }
        public DateTimeOffset? To { get; init; }
        public string? Technician { get; init; }
        public Guid? CompanyId { get; init; }
        public Guid? ConnectionId { get; init; }
        public string? Priority { get; init; }

        // Optional weight overrides (configurable score model).
        public double? WSla { get; init; }
        public double? WResolution { get; init; }
        public double? WCsat { get; init; }
        public double? WFirstResponse { get; init; }
        public double? WReopen { get; init; }
        public double? WWorklog { get; init; }
        public double? WDocumentation { get; init; }

        public MetricsFilter ToFilter() => new()
        {
            From = From, To = To, TechnicianExternalId = Technician,
            ClientCompanyId = CompanyId, PsaConnectionId = ConnectionId, Priority = Priority,
        };

        public ProductivityWeights ToWeights()
        {
            var d = ProductivityWeights.Default;
            return new ProductivityWeights
            {
                SlaCompliance = WSla ?? d.SlaCompliance,
                ResolutionRate = WResolution ?? d.ResolutionRate,
                CustomerSatisfaction = WCsat ?? d.CustomerSatisfaction,
                FirstResponse = WFirstResponse ?? d.FirstResponse,
                ReopenScore = WReopen ?? d.ReopenScore,
                WorklogQuality = WWorklog ?? d.WorklogQuality,
                DocumentationQuality = WDocumentation ?? d.DocumentationQuality,
            };
        }
    }
}
