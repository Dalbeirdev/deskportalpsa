using Desk.Domain.Enums;

namespace Desk.Application.Admin;

/// <summary>Appends an immutable audit entry for the current actor + tenant. Detail is redacted of secrets.</summary>
public interface IAuditWriter
{
    Task WriteAsync(string action, string entityType, string? entityId, object? detail = null, CancellationToken ct = default);
}

/// <summary>
/// PSA connection administration. Creating a connection writes its credentials to the secret store
/// and persists only the returned reference — the raw secret is never stored on the row, returned to
/// a caller, or logged. Every mutation is audited.
/// </summary>
public interface IConnectionAdminService
{
    Task<IReadOnlyList<ConnectionSummary>> ListAsync(CancellationToken ct = default);
    Task<ConnectionSummary> CreateAsync(CreateConnectionInput input, CancellationToken ct = default);
    Task SetEnabledAsync(Guid connectionId, bool enabled, CancellationToken ct = default);

    /// <summary>Live-tests a saved connection against its PSA, updates its health status, and audits it.</summary>
    Task<ConnectionTestResultDto> TestAsync(Guid connectionId, CancellationToken ct = default);

    /// <summary>Updates a connection's settings and, if new credentials are supplied, rotates them in the store. Audited.</summary>
    Task<ConnectionSummary> UpdateAsync(Guid connectionId, UpdateConnectionInput input, CancellationToken ct = default);

    /// <summary>Discovers boards/queues, statuses, priorities and categories live from the connected PSA.</summary>
    Task<ConnectionFieldsDto> GetFieldsAsync(Guid connectionId, CancellationToken ct = default);
}

/// <summary>
/// Field-mapping administration. Every upsert captures an immutable version snapshot of the mapping
/// set and writes an audit entry; rollback restores a prior snapshot (also audited).
/// </summary>
public interface IMappingAdminService
{
    Task<IReadOnlyList<MappingRuleDto>> ListAsync(ProviderType provider, CancellationToken ct = default);
    Task<MappingRuleDto> UpsertAsync(UpsertMappingInput input, string? changeNote, CancellationToken ct = default);
    Task<IReadOnlyList<MappingVersionDto>> VersionsAsync(ProviderType provider, Guid? connectionId, CancellationToken ct = default);
    Task RollbackAsync(Guid versionId, CancellationToken ct = default);
}

public interface IJobMonitorService
{
    Task<IReadOnlyList<JobSummary>> ListAsync(BackgroundJobStatus? status, CancellationToken ct = default);
    /// <summary>Requeues a dead-lettered job for another attempt. Audited.</summary>
    Task ReprocessAsync(Guid jobId, CancellationToken ct = default);
}

public interface IIntegrationHealthService
{
    Task<IReadOnlyList<ConnectionHealthDto>> SnapshotAsync(CancellationToken ct = default);
}

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditEntryDto>> ListAsync(int take = 100, string? action = null, CancellationToken ct = default);
}

public interface IUserAdminService
{
    Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct = default);
    Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
}
