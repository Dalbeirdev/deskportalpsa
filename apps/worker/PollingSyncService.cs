using Desk.Application.Connectors;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tenancy;
using Desk.PsaCore.Models;
using Microsoft.EntityFrameworkCore;

namespace Desk.Worker;

/// <summary>
/// Scheduled reconciliation poller. For each enabled connection it fetches tickets modified since
/// the last successful sync (incremental via the provider cursor) — the safety net that catches
/// anything missed by webhooks. Phase 3 wires the framework (resolve connector, page, update health);
/// normalization + persistence of the fetched tickets is the Phase-4 sync engine.
/// </summary>
public sealed class PollingSyncService(
    IServiceProvider services,
    ILogger<PollingSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Polling sync service started; interval {Interval}m", PollInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollConnectionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Polling cycle failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PollConnectionsAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetPlatformScope();

        var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<IConnectorResolver>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var connections = await db.PsaConnections
            .Where(c => c.IsEnabled && c.Status != ConnectionStatus.Failed)
            .ToListAsync(ct);

        foreach (var connection in connections)
        {
            try
            {
                var connector = await resolver.ResolveAsync(connection.Id, ct);
                var filter = new TicketFilter
                {
                    ModifiedSince = connection.LastSuccessfulSyncAt,
                    PageSize = connection.RateLimitPerMinute > 0 ? 100 : 50,
                };

                var page = await connector.GetTicketsAsync(filter, ct);
                logger.LogInformation("Polled {Count} modified ticket(s) from {Connection}",
                    page.Items.Count, connection.Name);

                connection.LastHealthCheckAt = clock.GetUtcNow();
                connection.Status = ConnectionStatus.Healthy;
                connection.LastError = null;
                // Persistence of normalized tickets is handled by the Phase-4 sync engine.
            }
            catch (Exception ex)
            {
                connection.Status = ConnectionStatus.Degraded;
                connection.LastError = ex.Message;
                logger.LogWarning(ex, "Polling failed for connection {Connection}", connection.Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
