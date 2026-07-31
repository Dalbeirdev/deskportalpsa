using Desk.Application.ControlPanel;
using Desk.Infrastructure.Tenancy;

namespace Desk.Worker;

/// <summary>
/// Ticks on an interval and generates any report schedules that have fallen due, across all tenants.
/// Runs under platform scope so a single pass covers every MSP organization. Delivery is handled by
/// the configured <see cref="IReportDelivery"/>; runs are always stored for in-portal download.
/// </summary>
public sealed class ScheduledReportService(
    IServiceProvider services,
    ILogger<ScheduledReportService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled report service started; interval {Interval}m", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().SetPlatformScope();
                var runner = scope.ServiceProvider.GetRequiredService<IScheduledReportRunner>();
                await runner.RunDueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled report cycle failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
