using Desk.Domain.Sync;

namespace Desk.Application.Jobs;

/// <summary>Handles one kind of background job. Registered by <see cref="JobType"/>.</summary>
public interface IJobHandler
{
    string JobType { get; }
    Task HandleAsync(BackgroundJob job, CancellationToken ct = default);
}
