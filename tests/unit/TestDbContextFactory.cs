using Desk.Application.Abstractions;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Desk.Tests.Unit;

/// <summary>Builds a DeskDbContext over a shared in-memory database with a chosen tenant scope.</summary>
internal static class TestDbContextFactory
{
    public static DeskDbContext ForTenant(string dbName, Guid organizationId)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(organizationId);
        return Build(dbName, tenant);
    }

    public static DeskDbContext ForPlatform(string dbName)
    {
        var tenant = new TenantContext();
        tenant.SetPlatformScope();
        return Build(dbName, tenant);
    }

    /// <summary>Context with no scope established — used to prove the fail-closed default.</summary>
    public static DeskDbContext Unscoped(string dbName) => Build(dbName, new TenantContext());

    private static DeskDbContext Build(string dbName, ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<DeskDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new DeskDbContext(options, tenant, TimeProvider.System);
    }
}
