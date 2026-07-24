using Desk.Application.Abstractions;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Desk.Worker;

/// <summary>
/// Phase-2 skeleton of the background job processor. It claims due jobs and (for now) logs
/// them; retry/backoff/dead-letter and the concrete job handlers (sync, polling, reconciliation)
/// arrive in Phase 3. Each job is processed under an explicit tenant scope so all data access
/// respects isolation exactly as the API does.
/// </summary>
public sealed class BackgroundJobPollingService(
    IServiceProvider services,
    ILogger<BackgroundJobPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job poller started; interval {Interval}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background poll cycle failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();

        // The poller runs under platform scope to discover jobs across all tenants; each job's
        // handler will narrow to that job's tenant before touching tenant-scoped data.
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetPlatformScope();
        var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();

        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var due = await db.BackgroundJobs
            .Where(j => j.Status == BackgroundJobStatus.Queued
                        && (j.NextAttemptAt == null || j.NextAttemptAt <= now))
            .OrderBy(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (due.Count == 0) return;
        logger.LogInformation("Claimed {Count} due background job(s)", due.Count);
        // Handler dispatch is implemented in Phase 3.
    }
}
