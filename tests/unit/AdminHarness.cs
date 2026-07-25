using Desk.Application.Abstractions;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Secrets;
using Desk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Desk.Tests.Unit;

/// <summary>Stub authenticated user for admin service tests.</summary>
public sealed class TestCurrentUser(Guid? org, string subject = "admin-sub", string name = "Admin User") : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? Subject => subject;
    public string? Email => "admin@test";
    public string? DisplayName => name;
    public Guid? OrganizationId => org;
    public IReadOnlySet<string> Permissions => new HashSet<string>();
    public bool HasPermission(string permissionKey) => true;
}

/// <summary>
/// Wires a DeskDbContext, tenant context, current user, secret store, and audit writer that all
/// share the same tenant scope — mirroring how a real admin request is composed.
/// </summary>
public sealed class AdminHarness
{
    public required DeskDbContext Db { get; init; }
    public required TenantContext Tenant { get; init; }
    public required TestCurrentUser User { get; init; }
    public required InMemorySecretStore Secrets { get; init; }
    public required TestClock Clock { get; init; }

    public static AdminHarness Create(Guid org)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(org);
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<DeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new DeskDbContext(options, tenant, clock);
        return new AdminHarness
        {
            Db = db, Tenant = tenant, User = new TestCurrentUser(org),
            Secrets = new InMemorySecretStore(), Clock = clock,
        };
    }
}
