namespace Desk.Domain.Common;

/// <summary>
/// Base for all persisted entities. Uses a GUID surrogate key so that external
/// PSA identifiers never leak into portal-internal references.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Marks an entity as belonging to exactly one MSP organization (tenant).
/// The EF Core global query filter enforces isolation on every entity that
/// implements this interface — there is no way to query one without a tenant scope.
/// </summary>
public interface ITenantScoped
{
    Guid MspOrganizationId { get; set; }
}

/// <summary>Convenience base for tenant-scoped entities.</summary>
public abstract class TenantEntity : BaseEntity, ITenantScoped
{
    public Guid MspOrganizationId { get; set; }
}
