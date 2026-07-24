namespace Desk.PsaCore.Contracts;

public enum AuthenticationType
{
    ApiKey,
    OAuth2ClientCredentials,
    OAuth2AuthorizationCode,
    BasicAuth,
    PersonalAccessToken,
}

/// <summary>
/// Declares exactly what a provider connector supports. The frontend and sync engine read
/// this matrix to hide/disable unsupported functions rather than assuming feature parity
/// (Integration Plan: "Create a provider capability matrix instead of assuming all
/// integrations are identical"). Never force behaviour a provider cannot do.
/// </summary>
public record ProviderCapabilities
{
    public bool SupportsTicketCreate { get; init; }
    public bool SupportsTicketUpdate { get; init; }
    public bool SupportsTicketDelete { get; init; }
    public bool SupportsPublicNotes { get; init; }
    public bool SupportsPrivateNotes { get; init; }
    public bool SupportsAttachments { get; init; }
    public bool SupportsTimeEntries { get; init; }
    public bool SupportsAssets { get; init; }
    public bool SupportsContracts { get; init; }
    public bool SupportsSlaData { get; init; }
    public bool SupportsCustomFields { get; init; }
    public bool SupportsInboundWebhooks { get; init; }
    public bool SupportsOutboundWebhooks { get; init; }
    public bool SupportsIncrementalSync { get; init; }
    public bool SupportsBulkRead { get; init; }
    public bool SupportsBulkWrite { get; init; }
    public bool SupportsCompanies { get; init; }
    public bool SupportsContacts { get; init; }
    public bool SupportsTechnicians { get; init; }
    public bool SupportsTeams { get; init; }
    public bool SupportsQueues { get; init; }

    public int? MaximumPageSize { get; init; }
    public long? MaximumAttachmentSize { get; init; }
    public string? RateLimitModel { get; init; }
    public IReadOnlyList<AuthenticationType> AuthenticationTypes { get; init; }
        = Array.Empty<AuthenticationType>();
}
