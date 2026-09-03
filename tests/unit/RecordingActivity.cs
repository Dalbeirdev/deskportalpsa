using Desk.Application.Analytics;

namespace Desk.Tests.Unit;

/// <summary>
/// Captures what was recorded instead of persisting it, so a test can assert the ACTIVITY a write
/// produced as well as the write itself. Also stands in wherever a service needs a recorder but the
/// test is about something else.
/// </summary>
public sealed class RecordingActivity : IActivityRecorder
{
    public List<ActivityRecord> Records { get; } = [];

    public Task RecordAsync(ActivityRecord record, CancellationToken ct = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task RecordManyAsync(IReadOnlyList<ActivityRecord> records, CancellationToken ct = default)
    {
        Records.AddRange(records);
        return Task.CompletedTask;
    }
}
