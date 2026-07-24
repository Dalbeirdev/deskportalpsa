using Desk.Application.Jobs;
using Desk.Domain.Sync;

namespace Desk.Worker;

/// <summary>
/// Phase-3 placeholder handler for inbound provider events. It acknowledges the job so the
/// retry/dead-letter plumbing is exercised end-to-end; the concrete apply-to-ticket logic
/// (map fields, reconcile status/notes, guard against echoes) lands with the sync engine in Phase 4.
/// </summary>
public sealed class InboundEventJobHandler(ILogger<InboundEventJobHandler> logger) : IJobHandler
{
    public string JobType => "sync.inbound-event";

    public Task HandleAsync(BackgroundJob job, CancellationToken ct = default)
    {
        logger.LogInformation("Handling inbound event job {JobId} for org {Org}: {Payload}",
            job.Id, job.MspOrganizationId, job.PayloadJson);
        return Task.CompletedTask;
    }
}
