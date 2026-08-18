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

/// <summary>
/// Marks an entity that belongs to a tenant OR is global/built-in (nullable org id) — e.g. a
/// system-provided row visible to every tenant alongside each tenant's own rows of the same kind.
/// The matching query filter treats a null org id as "visible everywhere."
///
/// Deliberately NOT applied to <see cref="Identity.AppUser"/> or <see cref="Identity.Role"/>: both
/// are looked up during authentication before any tenant scope has been established (the org claim
/// that establishes tenant scope is itself derived from the AppUser lookup), so a filter keyed on
/// "current tenant" would return zero rows at exactly the point that lookup needs to succeed.
/// Entities implementing this interface must only ever be queried from within an already-tenant-
/// scoped request.
/// </summary>
public interface INullableTenantScoped
{
    Guid? MspOrganizationId { get; set; }
}
