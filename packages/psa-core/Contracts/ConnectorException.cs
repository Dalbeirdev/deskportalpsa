namespace Desk.PsaCore.Contracts;

/// <summary>Classifies why a connector call failed, so the resilience layer can react correctly.</summary>
public enum ConnectorFailureKind
{
    /// <summary>Credentials rejected — do not retry; surface for remediation.</summary>
    Authentication,
    /// <summary>Caller lacks rights in the PSA — do not retry.</summary>
    PermissionDenied,
    /// <summary>Provider rate limit hit — retry after <see cref="ConnectorException.RetryAfter"/>.</summary>
    RateLimited,
    /// <summary>Network/timeout — transient, safe to retry.</summary>
    Timeout,
    /// <summary>Referenced entity does not exist — do not retry.</summary>
    NotFound,
    /// <summary>Malformed request the provider rejected — do not retry.</summary>
    InvalidRequest,
    /// <summary>Provider-side 5xx — transient, safe to retry.</summary>
    ProviderError,
}

/// <summary>Uniform error surface every connector throws, regardless of the underlying PSA SDK.</summary>
public sealed class ConnectorException(ConnectorFailureKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ConnectorFailureKind Kind { get; } = kind;

    /// <summary>Hint from the provider for when to retry (rate limiting).</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Whether the resilience layer should retry this failure.</summary>
    public bool IsTransient => Kind is ConnectorFailureKind.Timeout
        or ConnectorFailureKind.ProviderError
        or ConnectorFailureKind.RateLimited;
}
