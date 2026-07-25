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
    string? LastError);

public sealed record CreateConnectionInput(
    string Name,
    ProviderType Provider,
    string ApiEndpoint,
    string? TenantIdentifier,
    IReadOnlyDictionary<string, string> Credentials,
    string? TimeZone);

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

public sealed record UserSummary(Guid Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles);
