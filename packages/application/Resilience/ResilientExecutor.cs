using Desk.PsaCore.Contracts;

namespace Desk.Application.Resilience;

/// <summary>Runs an operation with retry + optional circuit breaking. Only transient failures retry.</summary>
public interface IResilientExecutor
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy policy,
        CircuitBreaker? breaker = null,
        CancellationToken ct = default);
}

public sealed class ResilientExecutor : IResilientExecutor
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<double> _jitter;

    /// <param name="delay">Injectable so tests run without real waits. Defaults to Task.Delay.</param>
    /// <param name="jitter">Injectable jitter source in [0,1); deterministic in tests.</param>
    public ResilientExecutor(
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<double>? jitter = null)
    {
        _delay = delay ?? Task.Delay;
        _jitter = jitter ?? (() => 0.0);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        RetryPolicy policy,
        CircuitBreaker? breaker = null,
        CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (breaker is not null && !breaker.CanExecute())
                throw new CircuitOpenException("Circuit is open; refusing call to a failing provider.");

            try
            {
                var result = await operation(ct);
                breaker?.OnSuccess();
                return result;
            }
            catch (ConnectorException ex)
            {
                breaker?.OnFailure();

                // Non-transient, or attempts exhausted → give up and surface the error.
                if (!ex.IsTransient || attempt >= policy.MaxAttempts)
                    throw;

                var wait = ex.RetryAfter ?? policy.ComputeDelay(attempt, _jitter());
                await _delay(wait, ct);
            }
            catch (Exception)
            {
                breaker?.OnFailure();
                throw; // unknown errors are never retried
            }
        }
    }
}
