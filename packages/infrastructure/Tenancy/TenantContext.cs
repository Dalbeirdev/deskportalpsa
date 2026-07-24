using Desk.Application.Abstractions;

namespace Desk.Infrastructure.Tenancy;

/// <summary>
/// Scoped implementation of the tenant context. Set once during request/job setup and read
/// by the DbContext global query filter for the remainder of the unit of work.
/// </summary>
public sealed class TenantContext : ISettableTenantContext
{
    private Guid? _organizationId;
    private bool _platformScope;
    private bool _locked;

    public Guid? OrganizationId => _organizationId;
    public bool IsPlatformScope => _platformScope;
    public bool HasScope => _platformScope || _organizationId.HasValue;

    public void SetTenant(Guid organizationId)
    {
        Guard();
        _organizationId = organizationId;
        _platformScope = false;
        _locked = true;
    }

    public void SetPlatformScope()
    {
        Guard();
        _platformScope = true;
        _organizationId = null;
        _locked = true;
    }

    // The scope is immutable once established, so a request cannot pivot tenants mid-flight.
    private void Guard()
    {
        if (_locked)
            throw new InvalidOperationException("Tenant scope has already been established for this context.");
    }
}
