namespace Desk.PsaCore.Models;

/// <summary>Normalized company/account/organization as returned by any provider.</summary>
public record ExternalOrganization(string ExternalId, string Name, bool IsActive);

/// <summary>Normalized contact/user/caller.</summary>
public record ExternalContact(string ExternalId, string Email, string DisplayName, bool IsActive);

/// <summary>Normalized technician/resource/member/agent.</summary>
public record ExternalTechnician(string ExternalId, string Email, string DisplayName, bool IsActive);

/// <summary>A selectable option for a picklist field (status/priority/queue/category).</summary>
public record ExternalFieldOption(string Value, string Label, bool IsActive = true);

/// <summary>Definition of a provider custom field discovered at runtime.</summary>
public record ExternalFieldDefinition(
    string Key,
    string Label,
    string DataType,
    bool IsRequired,
    IReadOnlyList<ExternalFieldOption>? Options = null);

/// <summary>Normalized ticket returned from a provider read.</summary>
public record UnifiedTicket
{
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Category { get; init; }
    public string? Subcategory { get; init; }
    public string? QueueOrBoard { get; init; }
    public string? AssignedTechnicianExternalId { get; init; }
    public string? RequesterExternalId { get; init; }
    public string? RequesterName { get; init; }
    public string? RequesterEmail { get; init; }
    public DateTimeOffset? SlaDueAt { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public IReadOnlyDictionary<string, string?> CustomFields { get; init; }
        = new Dictionary<string, string?>();
}

/// <summary>Request payload to create a ticket in a provider (portal → PSA).</summary>
public record UnifiedTicketCreateRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Category { get; init; }
    public string? QueueOrBoard { get; init; }
    public required string ExternalCompanyId { get; init; }
    public string? RequesterExternalId { get; init; }
    public string? RequesterEmail { get; init; }
    /// <summary>Idempotency key so retried creates never duplicate a PSA ticket.</summary>
    public required string IdempotencyKey { get; init; }
    public IReadOnlyDictionary<string, string?> CustomFields { get; init; }
        = new Dictionary<string, string?>();
}

/// <summary>Partial update payload (only set fields are written).</summary>
public record UnifiedTicketUpdate
{
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Category { get; init; }
    public string? QueueOrBoard { get; init; }
    public string? AssignedTechnicianExternalId { get; init; }
    public required string IdempotencyKey { get; init; }
}

public record UnifiedTicketNote(
    string? ExternalId,
    string AuthorName,
    string Body,
    bool IsPublic,
    DateTimeOffset CreatedAt);

public record UnifiedTicketNoteCreateRequest(string Body, bool IsPublic, string IdempotencyKey);

public record UnifiedTimeEntry(
    string ExternalId,
    string TechnicianExternalId,
    decimal Hours,
    bool Billable,
    DateTimeOffset EntryDate,
    string? Notes);

/// <summary>How a time entry is charged. Maps to ConnectWise billableOption / Autotask billing flags.</summary>
public enum BillableOption { Billable, DoNotBill, NoCharge }

/// <summary>Request to log time against a provider ticket. WorkType/WorkRole/Member are optional in
/// Phase 1 (wired to discovery + mapping in a later phase); Hours + Billable are the core inputs.</summary>
public record UnifiedTimeEntryCreateRequest(
    decimal Hours,
    string? WorkType,
    string? WorkRole,
    BillableOption Billable,
    string? Notes,
    string? MemberIdentifier);

public record UnifiedAttachment(
    string ExternalId,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>An attachment already scanned + staged in object storage, ready to push to a PSA.</summary>
public record SecureAttachment(
    string FileName,
    string ContentType,
    long SizeBytes,
    string StorageObjectKey);

/// <summary>Cursor/offset filter for paginated reads.</summary>
public record TicketFilter
{
    public DateTimeOffset? ModifiedSince { get; init; }
    public string? ExternalCompanyId { get; init; }
    public int PageSize { get; init; } = 100;
    public string? Cursor { get; init; }
}

/// <summary>Provider-agnostic paginated result.</summary>
public record PaginatedResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
