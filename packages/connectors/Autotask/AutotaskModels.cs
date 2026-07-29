using System.Text.Json.Serialization;

namespace Desk.Connectors.Autotask;

/// <summary>Autotask REST credentials. Resolved from Vault by the factory — never persisted in the DB.</summary>
public sealed record AutotaskCredentials(string ApiIntegrationCode, string UserName, string Secret);

/// <summary>Per-connection Autotask settings.</summary>
public sealed class AutotaskConnectorConfig
{
    /// <summary>Zone base URL, e.g. https://webservices2.autotask.net/atservicesrest/ (trailing slash).</summary>
    public required string BaseUrl { get; init; }
    public required AutotaskCredentials Credentials { get; init; }
    public string WebhookSecret { get; init; } = "";
    public TimeSpan WebhookMaxSkew { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>publish value the connector writes for a public (client-visible) note.</summary>
    public int PublicPublishValue { get; init; } = 1;
    /// <summary>publish value used for internal-only notes (never mirrored to the portal).</summary>
    public int InternalPublishValue { get; init; } = 2;
}

// ---- wire DTOs (subset of the Autotask REST schema the connector uses) ----

internal sealed class AtQueryResult<T>
{
    [JsonPropertyName("items")] public List<T> Items { get; set; } = [];
    [JsonPropertyName("pageDetails")] public AtPageDetails? PageDetails { get; set; }
}

internal sealed class AtPageDetails
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("nextPageUrl")] public string? NextPageUrl { get; set; }
}

internal sealed class AtItemResult<T>
{
    [JsonPropertyName("item")] public T? Item { get; set; }
}

internal sealed class AtCreateResult
{
    [JsonPropertyName("itemId")] public long ItemId { get; set; }
}

internal sealed class AtCompany
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("companyName")] public string? CompanyName { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
}

internal sealed class AtContact
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("companyID")] public long CompanyId { get; set; }
    [JsonPropertyName("emailAddress")] public string? EmailAddress { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
}

internal sealed class AtResource
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
}

internal sealed class AtTicket
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("priority")] public string? Priority { get; set; }
    [JsonPropertyName("queueID")] public string? QueueId { get; set; }
    [JsonPropertyName("ticketCategory")] public string? Category { get; set; }
    [JsonPropertyName("companyID")] public long CompanyId { get; set; }
    [JsonPropertyName("assignedResourceID")] public string? AssignedResourceId { get; set; }
    [JsonPropertyName("createDate")] public DateTimeOffset? CreateDate { get; set; }
    [JsonPropertyName("lastActivityDate")] public DateTimeOffset? LastActivityDate { get; set; }
    [JsonPropertyName("resolvedDateTime")] public DateTimeOffset? ResolvedDateTime { get; set; }
}

internal sealed class AtTicketNote
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("ticketID")] public long TicketId { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("publish")] public int Publish { get; set; }
    [JsonPropertyName("createDateTime")] public DateTimeOffset? CreateDateTime { get; set; }
}

internal sealed class AtFieldInfoResult
{
    [JsonPropertyName("fields")] public List<AtFieldInfo> Fields { get; set; } = [];
}

internal sealed class AtFieldInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("picklistValues")] public List<AtPicklistValue> PicklistValues { get; set; } = [];
}

internal sealed class AtPicklistValue
{
    [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
}
