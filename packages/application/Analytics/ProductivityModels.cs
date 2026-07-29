namespace Desk.Application.Analytics;

/// <summary>
/// The seven productivity components (each a 0-100 performance score). A null component was not
/// measured for the period and is excluded from the weighted score (weights renormalize over the
/// components that ARE present) rather than counted as zero.
/// </summary>
public sealed record ProductivityComponents
{
    public double? SlaCompliance { get; init; }
    public double? ResolutionRate { get; init; }
    public double? CustomerSatisfaction { get; init; }
    public double? FirstResponse { get; init; }
    /// <summary>Already expressed as a score where higher is better (i.e. fewer reopens).</summary>
    public double? ReopenScore { get; init; }
    public double? WorklogQuality { get; init; }
    public double? DocumentationQuality { get; init; }
}

/// <summary>Configurable weights for the productivity score. Defaults match the spec's model.</summary>
public sealed record ProductivityWeights
{
    public double SlaCompliance { get; init; } = 25;
    public double ResolutionRate { get; init; } = 20;
    public double CustomerSatisfaction { get; init; } = 15;
    public double FirstResponse { get; init; } = 15;
    public double ReopenScore { get; init; } = 10;
    public double WorklogQuality { get; init; } = 10;
    public double DocumentationQuality { get; init; } = 5;

    public static ProductivityWeights Default => new();
}

public sealed record ScoreContribution(string Component, double Score, double Weight, double WeightedPoints);

/// <summary>
/// Result of a productivity calculation. <see cref="Overall"/> is the weighted average over measured
/// components. <see cref="MeasuredWeightFraction"/> reports how much of the model's total weight was
/// actually measured — low coverage means the score rests on few signals and should be read with care.
/// </summary>
public sealed record ProductivityScore(
    double Overall,
    double MeasuredWeightFraction,
    IReadOnlyList<ScoreContribution> Breakdown)
{
    /// <summary>The mandated caveat, surfaced anywhere a score is shown.</summary>
    public const string Disclaimer =
        "Productivity scores are operational indicators only and must not be used as the sole basis " +
        "for employee performance decisions.";
}

// ---- metrics + filters ----

public sealed record MetricsFilter
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? TechnicianExternalId { get; init; }
    public Guid? ClientCompanyId { get; init; }
    public Guid? PsaConnectionId { get; init; }
    public string? Priority { get; init; }
}

public sealed record TechnicianMetrics
{
    public required string TechnicianExternalId { get; init; }
    public int Assigned { get; init; }
    public int Resolved { get; init; }
    public int Open { get; init; }
    public int Overdue { get; init; }
    public int WithinSla { get; init; }
    public int SlaEligible { get; init; }
    public double SlaCompliancePct { get; init; }
    public double AvgResolutionHours { get; init; }
    public decimal TimeWorkedHours { get; init; }
    public decimal BillableHours { get; init; }
    public decimal NonBillableHours { get; init; }
    public ProductivityComponents Components { get; init; } = new();
    public ProductivityScore? Score { get; init; }
}

public sealed record TeamComparisonRow(string TechnicianExternalId, int Resolved, double SlaCompliancePct, double? Score);

public sealed record TrendPoint(DateOnly Date, int Created, int Resolved);
