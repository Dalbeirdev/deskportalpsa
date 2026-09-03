using Desk.Application.Analytics;
using Desk.Infrastructure.Tenancy;

namespace Desk.Worker;

/// <summary>
/// Keeps the activity rollup current and the raw log bounded.
///
/// Runs under platform scope so one pass covers every tenant; the facts it writes carry the tenant
/// of the events behind them, so reads stay isolated by the global filter as usual.
///
/// Hourly rather than nightly because the recompute window is only seven days, and a dashboard that
/// is a day stale is a dashboard people stop trusting. The pass is cheap: it touches one week of
/// events, not the whole history.
/// </summary>
public sealed class ActivityRollupBackgroundService(
    IServiceProvider services,
    ILogger<ActivityRollupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Activity rollup started; interval {Interval}h", Interval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().SetPlatformScope();
                var rollup = scope.ServiceProvider.GetRequiredService<IActivityRollupService>();
                var result = await rollup.RunAsync(stoppingToken);

                // Logged even when nothing changed: an operator asking "is the rollup running"
                // deserves an answer that does not depend on there having been activity.
                logger.LogInformation(
                    "Activity rollup: {Days} days recomputed, {Facts} facts written, {Expired} raw events expired",
                    result.DaysProcessed, result.FactsWritten, result.RawEventsExpired);
            }
            catch (Exception ex)
            {
                // A failed pass must not stop the loop — the next one recomputes the same window,
                // so a transient failure repairs itself rather than leaving a permanent hole.
                logger.LogError(ex, "Activity rollup cycle failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
