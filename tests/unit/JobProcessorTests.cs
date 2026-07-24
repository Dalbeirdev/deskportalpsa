using Desk.Application.Jobs;
using Desk.Application.Resilience;
using Desk.Domain.Enums;
using Desk.Domain.Sync;
using Desk.Infrastructure.Jobs;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class JobProcessorTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private sealed class StubHandler(string type, Action? onHandle = null) : IJobHandler
    {
        public string JobType => type;
        public Task HandleAsync(BackgroundJob job, CancellationToken ct = default)
        {
            onHandle?.Invoke();
            return Task.CompletedTask;
        }
    }

    private static BackgroundJob NewJob(string type = "t", int maxAttempts = 3) => new()
    {
        MspOrganizationId = Org, JobType = type, PayloadJson = "{}",
        Status = BackgroundJobStatus.Queued, MaxAttempts = maxAttempts,
    };

    [Fact]
    public async Task Successful_job_is_marked_succeeded()
    {
        var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        var job = NewJob("ok"); db.BackgroundJobs.Add(job); await db.SaveChangesAsync();

        var processor = new JobProcessor(db, [new StubHandler("ok")], new TestClock());
        var status = await processor.ProcessAsync(job);

        status.Should().Be(BackgroundJobStatus.Succeeded);
        job.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Failing_job_requeues_with_backoff_until_max_then_dead_letters()
    {
        var clock = new TestClock();
        var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        var job = NewJob("boom", maxAttempts: 3); db.BackgroundJobs.Add(job); await db.SaveChangesAsync();

        var handler = new StubHandler("boom", () => throw new InvalidOperationException("nope"));
        var processor = new JobProcessor(db, [handler], clock, new RetryPolicy { UseJitter = false });

        // Attempt 1 → requeued with a future next-attempt.
        (await processor.ProcessAsync(job)).Should().Be(BackgroundJobStatus.Queued);
        job.NextAttemptAt.Should().NotBeNull();
        job.LastError.Should().Contain("nope");

        // Attempt 2 → still requeued.
        (await processor.ProcessAsync(job)).Should().Be(BackgroundJobStatus.Queued);

        // Attempt 3 → hits max, dead-lettered.
        (await processor.ProcessAsync(job)).Should().Be(BackgroundJobStatus.DeadLettered);
        job.Attempts.Should().Be(3);
    }

    [Fact]
    public async Task Unknown_job_type_dead_letters_immediately()
    {
        var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        var job = NewJob("no-handler"); db.BackgroundJobs.Add(job); await db.SaveChangesAsync();

        var processor = new JobProcessor(db, [], new TestClock());
        var status = await processor.ProcessAsync(job);

        status.Should().Be(BackgroundJobStatus.DeadLettered);
        job.LastError.Should().Contain("No handler");
    }
}
