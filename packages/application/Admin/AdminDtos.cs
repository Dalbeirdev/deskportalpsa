using Desk.Domain.Enums;

namespace Desk.Application.Admin;

/// <summary>PSA connection as shown to admins. Deliberately has NO credential field — secrets stay in Vault.</summary>
public sealed record ConnectionSummary(
    Guid Id,
    string Name,
    ProviderType Provider,
    string ApiEndpoint,
    string? TenantIdentifier,
    ConnectionStatus Status,
    bool IsEnabled,
    DateTimeOffset? LastSuccessfulSyncAt,
    string? LastError,
    // Counts drawn from what has actually synced, so the card cannot show a number the portal
    // does not hold. Trailing + defaulted so existing construction sites are unaffected.
    DateTimeOffset? LastHealthCheckAt = null,
    int TicketCount = 0,
    int CustomerCount = 0,
    int ContactCount = 0,
    string? LogoUrl = null);

public sealed record CreateConnectionInput(
    string Name,
    ProviderType Provider,
    string ApiEndpoint,
    string? TenantIdentifier,
    IReadOnlyDictionary<string, string> Credentials,
    string? TimeZone,
    string? LogoUrl = null);

public sealed record MappingRuleDto(
    Guid Id,
    ProviderType Provider,
    MappingScope Scope,
    Guid? PsaConnectionId,
    string PortalField,
    string? PortalValue,
    string ExternalField,
    string? ExternalValue,
    MappingDirection Direction,
    bool IsRequired,
    string? FallbackValue,
    bool IsActive,
    int Version);

public sealed record UpsertMappingInput(
    Guid? Id,
    ProviderType Provider,
    MappingScope Scope,
    Guid? PsaConnectionId,
    string PortalField,
    string? PortalValue,
    string ExternalField,
    string? ExternalValue,
    MappingDirection Direction,
    bool IsRequired,
    string? FallbackValue);

public sealed record MappingVersionDto(Guid Id, ProviderType Provider, Guid? PsaConnectionId, int Version, string ChangedByUserId, string? ChangeNote, DateTimeOffset CreatedAt);

public sealed record JobSummary(
    Guid Id,
    string JobType,
    BackgroundJobStatus Status,
    int Attempts,
    int MaxAttempts,
    DateTimeOffset? NextAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt);

public sealed record AuditEntryDto(
    Guid Id,
    string Action,
    string EntityType,
    string? EntityId,
    string? ActorDisplayName,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    string? DetailJson);

public sealed record ConnectionHealthDto(
    Guid ConnectionId,
    string Name,
    ProviderType Provider,
    ConnectionStatus Status,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastHealthCheckAt,
    int PendingJobs,
    int DeadLetterJobs,
    int FailedSyncEvents,
    string? LastError);

/// <summary>A ticket the portal holds that never reached the PSA.</summary>
public sealed record UnsyncedTicketDto(
    Guid TicketId,
    Guid PsaConnectionId,
    string ConnectionName,
    string Title,
    string? CustomerName,
    string SyncStatus,
    string? SyncError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAttemptAt);

/// <summary>How many are outstanding, and which they are.</summary>
public sealed record UnsyncedTicketsDto(int Count, IReadOnlyList<UnsyncedTicketDto> Tickets);

/// <summary>Outcome of a single resync attempt.</summary>
public sealed record ResyncResultDto(bool Success, Guid TicketId, string? ExternalTicketId, string? Error);

/// <summary>A role a staff user can hold, by id (for assignment) and name (for display).</summary>
public sealed record RoleOptionDto(Guid Id, string Name);

/// <summary>
/// SignInLinked distinguishes "can log in today" from "invited, binds on first login": a created
/// user has no IdP subject until they first sign in and their verified email matches.
/// </summary>
public sealed record UserSummary(
    Guid Id, string Email, string DisplayName, bool IsActive, bool SignInLinked,
    IReadOnlyList<RoleOptionDto> Roles);

public sealed record CreateStaffUserInput(string DisplayName, string Email, IReadOnlyList<Guid> RoleIds);

public sealed record ConnectionTestResultDto(bool Success, string? Message, double LatencyMs);

public sealed record UpdateConnectionInput(
    string Name,
    string ApiEndpoint,
    string? TenantIdentifier,
    string? TimeZone,
    bool IsEnabled,
    // When non-empty, replaces the stored credentials (rotation). Leave empty to keep existing.
    IReadOnlyDictionary<string, string>? Credentials,
    string? LogoUrl = null);

public sealed record FieldOptionDto(string Value, string Label);

/// <summary>Live field discovery for a connection: service boards/queues, statuses, priorities, categories.</summary>
public sealed record ConnectionFieldsDto(
    IReadOnlyList<FieldOptionDto> QueuesOrBoards,
    IReadOnlyList<FieldOptionDto> Statuses,
    IReadOnlyList<FieldOptionDto> Priorities,
    IReadOnlyList<FieldOptionDto> Categories,
    IReadOnlyList<FieldOptionDto> WorkTypes,
    IReadOnlyList<FieldOptionDto> WorkRoles,
    IReadOnlyList<FieldOptionDto> Technicians,
    IReadOnlyList<TechnicianCoverageDto> TechnicianCoverage);

/// <summary>A technician's role on one queue/board. Repeated per queue they cover.</summary>
public sealed record TechnicianCoverageDto(string TechnicianId, string? RoleId, string? RoleName, string? QueueOrBoardId);

/// <summary>Per-connection sync behaviour + import filters (what flows, and which tickets are ours).</summary>
public sealed record ConnectionSettingsDto(
    bool TwoWaySync, bool AutoImportNewTickets, bool ImportNotes, bool ImportSystemNotes, bool SyncAttachments,
    bool ImportOpenTickets, bool ImportClosedTickets,
    string? FilterCompanyIds, string? FilterQueueIds, string? FilterResourceIds, int? FilterActiveWithinDays,
    string? DefaultQueueOrBoardId, string? DefaultTicketType, string? DefaultIssueType, string? DefaultSubIssueType,
    string? DefaultTimeEntryResourceId, string? DefaultTimeEntryRoleId);
