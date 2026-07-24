namespace Desk.Application.Abstractions;

/// <summary>
/// The tenant scope for the current unit of work. Resolved once per request (from the
/// authenticated principal's org claim) or per background job (from the job payload).
/// The EF Core global query filter reads <see cref="OrganizationId"/> to constrain EVERY
/// tenant-scoped query — this is the single source of truth for isolation.
/// </summary>
public interface ITenantContext
{
    /// <summary>Current tenant, or null for an unauthenticated / not-yet-resolved context.</summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// True for platform super-administrators, who may operate across tenants. When true the
    /// global query filter is bypassed and the caller is responsible for explicit scoping.
    /// </summary>
    bool IsPlatformScope { get; }

    /// <summary>True when a concrete tenant (or platform scope) has been established.</summary>
    bool HasScope { get; }
}

/// <summary>Mutable tenant context used to set the scope during request/job setup.</summary>
public interface ISettableTenantContext : ITenantContext
{
    void SetTenant(Guid organizationId);
    void SetPlatformScope();
}
