using Desk.Domain.Common;
using Desk.Domain.Enums;

namespace Desk.Domain.Tenancy;

/// <summary>
/// A single configured connection to one PSA tenant. An MSP org may have many of these,
/// including multiple connections to the same provider (e.g. two Autotask tenants).
///
/// SECURITY: No API secret is ever stored on this row. <see cref="CredentialSecretRef"/>
/// is an opaque pointer into the secret store (HashiCorp Vault). See ISecretStore.
/// </summary>
public class PsaConnection : TenantEntity
{
    public required string Name { get; set; }
    public ProviderType Provider { get; set; }

    /// <summary>Base API endpoint for this PSA tenant.</summary>
    public required string ApiEndpoint { get; set; }

    /// <summary>Provider-specific tenant identifier (AT zone / CW company id / Halo instance).</summary>
    public string? TenantIdentifier { get; set; }

    /// <summary>Opaque Vault reference — NOT the secret itself. Masked in all UI and logs.</summary>
    public required string CredentialSecretRef { get; set; }

    public string TimeZone { get; set; } = "UTC";
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
    public DateTimeOffset? LastHealthCheckAt { get; set; }
    public string? LastError { get; set; }

    // Rate-limit + retry configuration (provider defaults may override).
    public int RateLimitPerMinute { get; set; } = 60;
    public int MaxRetries { get; set; } = 5;
    public int RetryBaseDelaySeconds { get; set; } = 2;

    public ICollection<ClientCompany> ClientCompanies { get; set; } = new List<ClientCompany>();
}
