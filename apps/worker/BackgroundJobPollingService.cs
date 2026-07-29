using Desk.Application.Jobs;
using Desk.Domain.Enums;
using Desk.Infrastructure.Jobs;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Desk.Worker;

/// <summary>
/// Claims due background jobs and runs each through <see cref="JobProcessor"/>, which applies the
/// retry / backoff / dead-letter policy. Runs under platform scope to discover jobs across all
/// tenants; each handler narrows to its job's tenant before touching tenant-scoped data.
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
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetPlatformScope();

        var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var handlers = scope.ServiceProvider.GetServices<IJobHandler>();
        var processor = new JobProcessor(db, handlers, clock);

        var now = clock.GetUtcNow();
        var due = await db.BackgroundJobs
            .Where(j => j.Status == BackgroundJobStatus.Queued
                        && (j.NextAttemptAt == null || j.NextAttemptAt <= now))
            .OrderBy(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (due.Count == 0) return;
        logger.LogInformation("Processing {Count} due background job(s)", due.Count);

        foreach (var job in due)
        {
            var result = await processor.ProcessAsync(job, ct);
            if (result == BackgroundJobStatus.DeadLettered)
                logger.LogWarning("Job {JobId} ({Type}) dead-lettered: {Error}", job.Id, job.JobType, job.LastError);
        }
    }
}
