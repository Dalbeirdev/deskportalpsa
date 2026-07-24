using Desk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Desk.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef migrations` / `database update`. Uses platform scope
/// (no tenant filtering) purely for schema generation — it never runs application queries.
/// </summary>
public sealed class DeskDbContextFactory : IDesignTimeDbContextFactory<DeskDbContext>
{
    public DeskDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DESK_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=desk_portal;Username=desk;Password=desk";

        var options = new DbContextOptionsBuilder<DeskDbContext>()
            .UseNpgsql(conn, o => o.MigrationsAssembly(typeof(DeskDbContextFactory).Assembly.FullName))
            .Options;

        return new DeskDbContext(options, new DesignTimeTenantContext(), TimeProvider.System);
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
        public bool HasScope => true;
    }
}
