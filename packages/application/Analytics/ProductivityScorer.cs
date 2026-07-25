namespace Desk.Application.Analytics;

/// <summary>Computes the weighted productivity score from components + configurable weights.</summary>
public interface IProductivityScorer
{
    ProductivityScore Calculate(ProductivityComponents components, ProductivityWeights weights);
}

/// <summary>
/// Weighted-average scorer. Only measured (non-null) components contribute; their weights are
/// renormalized so the result is always on a 0-100 scale regardless of how many components are
/// present. Each component score is clamped to [0,100] defensively. Deterministic and pure.
/// </summary>
public sealed class ProductivityScorer : IProductivityScorer
{
    public ProductivityScore Calculate(ProductivityComponents c, ProductivityWeights w)
    {
        var candidates = new (string Name, double? Score, double Weight)[]
        {
            ("SLA Compliance", c.SlaCompliance, w.SlaCompliance),
            ("Resolution Rate", c.ResolutionRate, w.ResolutionRate),
            ("Customer Satisfaction", c.CustomerSatisfaction, w.CustomerSatisfaction),
            ("First Response", c.FirstResponse, w.FirstResponse),
            ("Reopen Rate", c.ReopenScore, w.ReopenScore),
            ("Worklog Quality", c.WorklogQuality, w.WorklogQuality),
            ("Documentation Quality", c.DocumentationQuality, w.DocumentationQuality),
        };

        var measured = candidates.Where(x => x.Score is not null && x.Weight > 0).ToList();
        var totalWeight = candidates.Where(x => x.Weight > 0).Sum(x => x.Weight);
        var measuredWeight = measured.Sum(x => x.Weight);

        if (measuredWeight <= 0)
            return new ProductivityScore(0, 0, []);

        var breakdown = measured
            .Select(x =>
            {
                var score = Math.Clamp(x.Score!.Value, 0, 100);
                var normalizedWeight = x.Weight / measuredWeight;
                return new ScoreContribution(x.Name, score, x.Weight, score * normalizedWeight);
            })
            .ToList();

        var overall = Math.Round(breakdown.Sum(b => b.WeightedPoints), 2);
        var coverage = totalWeight > 0 ? Math.Round(measuredWeight / totalWeight, 4) : 0;
        return new ProductivityScore(overall, coverage, breakdown);
    }
}
