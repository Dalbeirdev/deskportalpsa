using System.Text.Json.Serialization;

namespace Desk.Connectors.ConnectWise;

/// <summary>ConnectWise Manage API credentials. Resolved from Vault by the factory.</summary>
public sealed record ConnectWiseCredentials(string CompanyId, string PublicKey, string PrivateKey, string ClientId);

public sealed class ConnectWiseConnectorConfig
{
    /// <summary>API base, e.g. https://api-na.myconnectwise.net/v4_6_release/apis/3.0/ (trailing slash).</summary>
    public required string BaseUrl { get; init; }
    public required ConnectWiseCredentials Credentials { get; init; }
    public string WebhookSecret { get; init; } = "";
    public TimeSpan WebhookMaxSkew { get; init; } = TimeSpan.FromMinutes(5);
}

// ---- wire DTOs (subset of the ConnectWise Manage schema). CW nests references as {id, name}. ----

internal sealed class CwRef
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

internal sealed class CwCompany
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("deletedFlag")] public bool DeletedFlag { get; set; }
}

internal sealed class CwContact
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("inactiveFlag")] public bool InactiveFlag { get; set; }
}

internal sealed class CwMember
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("primaryEmail")] public string? PrimaryEmail { get; set; }
    [JsonPropertyName("inactiveFlag")] public bool InactiveFlag { get; set; }
}

internal sealed class CwTicket
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("summary")] public string? Summary { get; set; }
    [JsonPropertyName("initialDescription")] public string? InitialDescription { get; set; }
    [JsonPropertyName("board")] public CwRef? Board { get; set; }
    [JsonPropertyName("status")] public CwRef? Status { get; set; }
    [JsonPropertyName("priority")] public CwRef? Priority { get; set; }
    [JsonPropertyName("type")] public CwRef? Type { get; set; }
    [JsonPropertyName("company")] public CwRef? Company { get; set; }
    [JsonPropertyName("owner")] public CwRef? Owner { get; set; }
    [JsonPropertyName("lastUpdated")] public DateTimeOffset? LastUpdated { get; set; }
    [JsonPropertyName("dateResolved")] public DateTimeOffset? DateResolved { get; set; }
}

internal sealed class CwTicketNote
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("internalAnalysisFlag")] public bool InternalAnalysisFlag { get; set; }
    [JsonPropertyName("detailDescriptionFlag")] public bool DetailDescriptionFlag { get; set; }
    [JsonPropertyName("customerUpdatedFlag")] public bool CustomerUpdatedFlag { get; set; }
    [JsonPropertyName("dateCreated")] public DateTimeOffset? DateCreated { get; set; }
    [JsonPropertyName("member")] public CwRef? Member { get; set; }
    // Set instead of "member" when a customer contact wrote the note (portal/email replies).
    [JsonPropertyName("contact")] public CwRef? Contact { get; set; }
}

internal sealed class CwTimeEntry
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("member")] public CwRef? Member { get; set; }
    [JsonPropertyName("actualHours")] public decimal? ActualHours { get; set; }
    [JsonPropertyName("billableOption")] public string? BillableOption { get; set; }
    [JsonPropertyName("timeStart")] public DateTimeOffset? TimeStart { get; set; }
    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("workType")] public CwRef? WorkType { get; set; }
}

internal sealed class CwConfiguration
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("type")] public CwRef? Type { get; set; }
    [JsonPropertyName("status")] public CwRef? Status { get; set; }
    [JsonPropertyName("serialNumber")] public string? SerialNumber { get; set; }
    [JsonPropertyName("tagNumber")] public string? TagNumber { get; set; }
}
