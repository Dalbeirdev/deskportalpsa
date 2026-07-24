using Desk.Application.Abstractions;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Secrets;
using Desk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Desk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDeskInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? Environment.GetEnvironmentVariable("DESK_DB_CONNECTION")
            ?? throw new InvalidOperationException("No Postgres connection string configured (ConnectionStrings:Postgres).");

        services.AddDbContext<DeskDbContext>(o =>
            o.UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(DeskDbContext).Assembly.FullName)));

        services.AddSingleton(TimeProvider.System);

        // One TenantContext instance per scope, exposed under both interfaces.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ISettableTenantContext>(sp => sp.GetRequiredService<TenantContext>());

        AddSecretStore(services, config);
        return services;
    }

    private static void AddSecretStore(IServiceCollection services, IConfiguration config)
    {
        var vaultAddress = config["Vault:Address"];
        var vaultToken = config["Vault:Token"];

        if (!string.IsNullOrWhiteSpace(vaultAddress) && !string.IsNullOrWhiteSpace(vaultToken))
        {
            var options = new VaultOptions
            {
                Address = vaultAddress,
                Token = vaultToken,
                MountPoint = config["Vault:MountPoint"] ?? "secret",
                PathPrefix = config["Vault:PathPrefix"] ?? "desk/psa-credentials",
            };
            services.AddSingleton(options);
            services.AddSingleton(VaultSecretStore.BuildClient(options));
            services.AddSingleton<ISecretStore, VaultSecretStore>();
        }
        else
        {
            // Dev/test fallback. Production startup asserts a real store is configured.
            services.AddSingleton<ISecretStore, InMemorySecretStore>();
        }
    }
}
