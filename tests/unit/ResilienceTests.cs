using Desk.Application.Resilience;
using Desk.PsaCore.Contracts;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class ResilienceTests
{
    // No-op delay so retries don't actually wait.
    private static ResilientExecutor Executor(List<TimeSpan>? waits = null) =>
        new(delay: (ts, _) => { waits?.Add(ts); return Task.CompletedTask; });

    private static readonly RetryPolicy Fast = new() { MaxAttempts = 5, BaseDelay = TimeSpan.FromMilliseconds(10), UseJitter = false };

    [Fact]
    public async Task Retries_transient_failures_then_succeeds()
    {
        var attempts = 0;
        var result = await Executor().ExecuteAsync(_ =>
        {
            attempts++;
            if (attempts < 3) throw new ConnectorException(ConnectorFailureKind.Timeout, "flaky");
            return Task.FromResult("ok");
        }, Fast);

        result.Should().Be("ok");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Does_not_retry_non_transient_failures()
    {
        var attempts = 0;
        var act = async () => await Executor().ExecuteAsync<string>(_ =>
        {
            attempts++;
            throw new ConnectorException(ConnectorFailureKind.Authentication, "bad creds");
        }, Fast);

        await act.Should().ThrowAsync<ConnectorException>().Where(e => e.Kind == ConnectorFailureKind.Authentication);
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task Gives_up_after_max_attempts()
    {
        var attempts = 0;
        var act = async () => await Executor().ExecuteAsync<string>(_ =>
        {
            attempts++;
            throw new ConnectorException(ConnectorFailureKind.ProviderError, "500");
        }, Fast with { MaxAttempts = 3 });

        await act.Should().ThrowAsync<ConnectorException>();
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Rate_limited_failure_honours_retry_after()
    {
        var waits = new List<TimeSpan>();
        var attempts = 0;
        await Executor(waits).ExecuteAsync(_ =>
        {
            attempts++;
            if (attempts == 1)
                throw new ConnectorException(ConnectorFailureKind.RateLimited, "429") { RetryAfter = TimeSpan.FromSeconds(7) };
            return Task.FromResult(1);
        }, Fast);

        waits.Should().ContainSingle().Which.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task Backoff_grows_exponentially()
    {
        var waits = new List<TimeSpan>();
        var attempts = 0;
        try
        {
            await Executor(waits).ExecuteAsync<string>(_ =>
            {
                attempts++;
                throw new ConnectorException(ConnectorFailureKind.Timeout, "t");
            }, new RetryPolicy { MaxAttempts = 4, BaseDelay = TimeSpan.FromMilliseconds(100), Multiplier = 2, UseJitter = false });
        }
        catch (ConnectorException) { /* expected */ }

        waits.Should().Equal(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400));
    }

    [Fact]
    public void Circuit_opens_after_threshold_then_sheds_load()
    {
        var clock = new TestClock();
        var cb = new CircuitBreaker(clock, failureThreshold: 3, openDuration: TimeSpan.FromSeconds(30));

        cb.OnFailure(); cb.OnFailure(); cb.OnFailure();

        cb.State.Should().Be(CircuitState.Open);
        cb.CanExecute().Should().BeFalse();
    }

    [Fact]
    public void Circuit_half_opens_after_cooldown_and_closes_on_success()
    {
        var clock = new TestClock();
        var cb = new CircuitBreaker(clock, failureThreshold: 2, openDuration: TimeSpan.FromSeconds(30));

        cb.OnFailure(); cb.OnFailure();
        cb.CanExecute().Should().BeFalse();

        clock.Advance(TimeSpan.FromSeconds(31));
        cb.CanExecute().Should().BeTrue();        // half-open probe allowed
        cb.State.Should().Be(CircuitState.HalfOpen);

        cb.OnSuccess();
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void Circuit_reopens_if_probe_fails()
    {
        var clock = new TestClock();
        var cb = new CircuitBreaker(clock, failureThreshold: 2, openDuration: TimeSpan.FromSeconds(30));

        cb.OnFailure(); cb.OnFailure();
        clock.Advance(TimeSpan.FromSeconds(31));
        cb.CanExecute().Should().BeTrue(); // half-open
        cb.OnFailure();                    // probe fails
        cb.State.Should().Be(CircuitState.Open);
    }
}
