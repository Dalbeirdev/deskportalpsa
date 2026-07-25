using System.Globalization;
using System.Text;
using Desk.Application.Analytics;
using Desk.Domain.Authorization;
using Desk.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Api.Controllers;

/// <summary>
/// Staff productivity dashboards (technician + manager). Team-wide views require the team
/// productivity permission; export mirrors the team query. Every response carries the operational-
/// indicator disclaimer so it travels with the numbers.
/// </summary>
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(ITechnicianMetricsService metrics) : ControllerBase
{
    [HttpGet("technician")]
    [RequirePermission(Permissions.ProductivityViewOwn)]
    public async Task<IActionResult> Technician([FromQuery] DashboardQuery q, CancellationToken ct)
    {
        var m = await metrics.ForTechnicianAsync(q.ToFilter(), q.ToWeights(), ct);
        return Ok(new { metrics = m, disclaimer = ProductivityScore.Disclaimer });
    }

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
