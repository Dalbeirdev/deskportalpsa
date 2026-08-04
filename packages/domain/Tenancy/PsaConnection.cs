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

    // ---- Sync behaviour: what flows automatically between the portal and this PSA ----

    /// <summary>Pull provider-side changes back into the portal. Off = portal→PSA writes only.</summary>
    public bool TwoWaySync { get; set; } = true;

    /// <summary>Create brand-new provider tickets in the portal on each sync (hands-free intake).</summary>
    public bool AutoImportNewTickets { get; set; } = true;

    /// <summary>Mirror provider notes into the portal thread. Requires <see cref="TwoWaySync"/>.</summary>
    public bool ImportNotes { get; set; } = true;

    /// <summary>Include machine-generated notes (workflow/SLA noise). Off keeps threads human-only.</summary>
    public bool ImportSystemNotes { get; set; }

    /// <summary>Upload portal attachments to the provider and mirror theirs back.</summary>
    public bool SyncAttachments { get; set; } = true;

    // ---- Import filters: which of the provider's tickets are ours to import ----

    public bool ImportOpenTickets { get; set; } = true;
    public bool ImportClosedTickets { get; set; }

    /// <summary>Comma-separated external ids; empty = no restriction. Queue = CW service board.</summary>
    public string? FilterCompanyIds { get; set; }
    public string? FilterQueueIds { get; set; }
    public string? FilterResourceIds { get; set; }

    /// <summary>Only import tickets active within this many days. Null/0 = no age limit.</summary>
    public int? FilterActiveWithinDays { get; set; }

    // ---- Ticket defaults: values sent when the portal creates a ticket in this PSA ----
    // Providers mandate different fields (Autotask requires a queue; ConnectWise a board), and the
    // portal's create form deliberately stays simple, so the connection supplies the rest.
    // Stored as the provider's own external ids, chosen from live field discovery.

    public string? DefaultQueueOrBoardId { get; set; }
    public string? DefaultTicketType { get; set; }
    public string? DefaultIssueType { get; set; }
    public string? DefaultSubIssueType { get; set; }

    // Rate-limit + retry configuration (provider defaults may override).
    public int RateLimitPerMinute { get; set; } = 60;
    public int MaxRetries { get; set; } = 5;
    public int RetryBaseDelaySeconds { get; set; } = 2;

    public ICollection<ClientCompany> ClientCompanies { get; set; } = new List<ClientCompany>();
}
