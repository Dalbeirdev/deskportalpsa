using Desk.Domain.Common;

namespace Desk.Domain.Assistant;

/// <summary>
/// Per-tenant assistant configuration. One row per MSP organization, and OFF until an administrator
/// turns it on: using it sends ticket text to a third party, and an MSP holds other companies' data.
/// That is a contractual decision for the MSP to make deliberately, never a default they inherit.
///
/// The API key lives in the same encrypted secret store as PSA credentials and is referenced here,
/// so it never appears on this row, in an API response, or in a log.
/// </summary>
public class AssistantSettings : TenantEntity
{
    public bool IsEnabled { get; set; }

    /// <summary>Reference into the secret store. Null until a key has been saved.</summary>
    public string? CredentialSecretRef { get; set; }

    /// <summary>Model id, so a tenant can move without a deploy.</summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>
    /// Whether internal notes and time entries may be sent. Default FALSE: those carry rates and
    /// private commentary, and the useful answers come from the public thread anyway.
    /// </summary>
    public bool IncludeInternalNotes { get; set; }
}
