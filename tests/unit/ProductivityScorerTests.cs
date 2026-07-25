using Desk.Application.Analytics;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class ProductivityScorerTests
{
    private readonly ProductivityScorer _scorer = new();

    [Fact]
    public void All_components_perfect_scores_100()
    {
        var c = new ProductivityComponents
        {
            SlaCompliance = 100, ResolutionRate = 100, CustomerSatisfaction = 100,
            FirstResponse = 100, ReopenScore = 100, WorklogQuality = 100, DocumentationQuality = 100,
        };
        var result = _scorer.Calculate(c, ProductivityWeights.Default);
        result.Overall.Should().Be(100);
        result.MeasuredWeightFraction.Should().Be(1);
    }

    [Fact]
    public void Unmeasured_components_are_excluded_and_weights_renormalize()
    {
        // Only SLA (weight 25) and Resolution (weight 20) measured → renormalize over 45.
        var c = new ProductivityComponents { SlaCompliance = 80, ResolutionRate = 90 };
        var result = _scorer.Calculate(c, ProductivityWeights.Default);

        // (80*25 + 90*20) / 45 = 84.44
        result.Overall.Should().BeApproximately(84.44, 0.01);
        result.MeasuredWeightFraction.Should().BeApproximately(0.45, 0.001);
        result.Breakdown.Should().HaveCount(2);
    }

    [Fact]
    public void Component_scores_are_clamped_to_0_100()
    {
        var c = new ProductivityComponents { SlaCompliance = 150, ResolutionRate = -20 };
        var result = _scorer.Calculate(c, new ProductivityWeights { SlaCompliance = 50, ResolutionRate = 50 });
        // clamps to 100 and 0 → average 50
        result.Overall.Should().Be(50);
    }

    [Fact]
    public void Configurable_weights_change_the_result()
    {
        var c = new ProductivityComponents { SlaCompliance = 100, ResolutionRate = 0 };
        var slaHeavy = _scorer.Calculate(c, new ProductivityWeights { SlaCompliance = 90, ResolutionRate = 10 });
        var resolutionHeavy = _scorer.Calculate(c, new ProductivityWeights { SlaCompliance = 10, ResolutionRate = 90 });

        slaHeavy.Overall.Should().Be(90);        // 100*0.9 + 0*0.1
        resolutionHeavy.Overall.Should().Be(10); // 100*0.1 + 0*0.9
        slaHeavy.Overall.Should().BeGreaterThan(resolutionHeavy.Overall);
    }

    [Fact]
    public void No_measured_components_scores_zero_with_zero_coverage()
    {
        var result = _scorer.Calculate(new ProductivityComponents(), ProductivityWeights.Default);
        result.Overall.Should().Be(0);
        result.MeasuredWeightFraction.Should().Be(0);
        result.Breakdown.Should().BeEmpty();
    }

    [Fact]
    public void A_component_with_zero_weight_never_contributes()
    {
        var c = new ProductivityComponents { SlaCompliance = 100, ResolutionRate = 0 };
        var result = _scorer.Calculate(c, new ProductivityWeights { SlaCompliance = 100, ResolutionRate = 0 });
        result.Overall.Should().Be(100); // resolution excluded by zero weight
        result.Breakdown.Should().ContainSingle();
    }
}
