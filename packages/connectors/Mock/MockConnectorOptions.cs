using Desk.Domain.Enums;
using Desk.PsaCore.Contracts;

namespace Desk.Connectors.Mock;

/// <summary>
/// Configures the in-memory mock connector, including fault injection so the certification suite
/// can exercise auth failure, rate limiting, timeouts, and permission-denied paths deterministically.
/// </summary>
public sealed class MockConnectorOptions
{
    public ProviderType Provider { get; set; } = ProviderType.ConnectWisePsa;

    /// <summary>Shared secret used to validate inbound webhook HMAC signatures.</summary>
    public string WebhookSecret { get; set; } = "mock-webhook-secret";

    /// <summary>Maximum accepted clock skew for a webhook timestamp.</summary>
    public TimeSpan WebhookMaxSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>When set, every call throws this failure kind (fault injection).</summary>
    public ConnectorFailureKind? FailEveryCallWith { get; set; }

    /// <summary>Number of leading calls that fail transiently before succeeding (retry testing).</summary>
    public int TransientFailuresBeforeSuccess { get; set; }
}
