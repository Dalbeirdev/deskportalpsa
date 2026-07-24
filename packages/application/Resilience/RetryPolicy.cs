namespace Desk.Application.Resilience;

/// <summary>Exponential-backoff retry configuration for a connector call.</summary>
public sealed record RetryPolicy
{
    public int MaxAttempts { get; init; } = 5;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);
    public double Multiplier { get; init; } = 2.0;
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(2);
    public bool UseJitter { get; init; } = true;

    /// <summary>Backoff for a given 1-based attempt number, capped at <see cref="MaxDelay"/>.</summary>
    public TimeSpan ComputeDelay(int attempt, double jitterFraction = 0)
    {
        var raw = BaseDelay.TotalMilliseconds * Math.Pow(Multiplier, Math.Max(0, attempt - 1));
        var capped = Math.Min(raw, MaxDelay.TotalMilliseconds);
        if (UseJitter)
            capped *= 1 + (jitterFraction * 0.2); // ±20% spread supplied by the caller
        return TimeSpan.FromMilliseconds(capped);
    }
}
