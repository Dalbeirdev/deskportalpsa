namespace Desk.Application.Resilience;

public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>Thrown when a call is attempted while the breaker is open.</summary>
public sealed class CircuitOpenException(string message) : Exception(message);

/// <summary>
/// Per-connection circuit breaker. After <see cref="_failureThreshold"/> consecutive failures it
/// opens for <see cref="_openDuration"/>, shedding load off a failing PSA; the first probe after
/// that window is allowed (half-open) and a success closes it again.
/// </summary>
public sealed class CircuitBreaker(TimeProvider clock, int failureThreshold = 5, TimeSpan? openDuration = null)
{
    private readonly int _failureThreshold = failureThreshold;
    private readonly TimeSpan _openDuration = openDuration ?? TimeSpan.FromSeconds(30);
    private readonly Lock _gate = new();

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;

    public CircuitState State { get { lock (_gate) return _state; } }

    /// <summary>Whether a call may proceed right now.</summary>
    public bool CanExecute()
    {
        lock (_gate)
        {
            if (_state == CircuitState.Open)
            {
                if (clock.GetUtcNow() - _openedAt >= _openDuration)
                {
                    _state = CircuitState.HalfOpen; // allow a single probe
                    return true;
                }
                return false;
            }
            return true;
        }
    }

    public void OnSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

    public void OnFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            if (_state == CircuitState.HalfOpen || _consecutiveFailures >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = clock.GetUtcNow();
            }
        }
    }
}
