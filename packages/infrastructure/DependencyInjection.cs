using Desk.Application.Abstractions;
using Desk.Application.Connectors;
using Desk.Application.Mapping;
using Desk.Application.Resilience;
using Desk.Application.Admin;
using Desk.Application.Analytics;
using Desk.Application.Attachments;
using Desk.Infrastructure.Attachments;
using Desk.Application.Sync;
using Desk.Application.Tickets;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Analytics;
using Desk.Infrastructure.Connectors;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tickets;
using Desk.PsaCore.Contracts;
using Desk.Infrastructure.Secrets;
using Desk.Infrastructure.Security;
using Desk.Infrastructure.Sync;
using Desk.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Desk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDeskInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Local mode runs with zero external dependencies (in-memory DB + secret store) for a
        // no-Docker demo. Otherwise, connect to Postgres as usual.
        var localMode = config.GetValue("LocalMode:Enabled", false);
        if (localMode)
        {
            services.AddDbContext<DeskDbContext>(o => o.UseInMemoryDatabase("desk-local"));
        }
        else
        {
            var connectionString = config.GetConnectionString("Postgres")
                ?? Environment.GetEnvironmentVariable("DESK_DB_CONNECTION")
                ?? throw new InvalidOperationException("No Postgres connection string configured (ConnectionStrings:Postgres).");
            services.AddDbContext<DeskDbContext>(o =>
                o.UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(DeskDbContext).Assembly.FullName)));
        }

        services.AddSingleton(TimeProvider.System);

        // One TenantContext instance per scope, exposed under both interfaces.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ISettableTenantContext>(sp => sp.GetRequiredService<TenantContext>());

        if (localMode)
            services.AddSingleton<ISecretStore, InMemorySecretStore>();
        else
            AddSecretStore(services, config);

        // Integration framework (Phase 3)
        services.AddSingleton<IMappingEngine, MappingEngine>();
        services.AddSingleton<IResilientExecutor>(_ => new ResilientExecutor());
        services.AddScoped<ISyncEventStore, SyncEventStore>();
        // Local mode resolves an in-process stub connector (no secret store, no external PSA);
        // otherwise the real per-provider factories are selected by ConnectorResolver.
        if (localMode)
            services.AddScoped<IConnectorResolver, LocalConnectorResolver>();
        else
            services.AddScoped<IConnectorResolver, ConnectorResolver>();

        // Sync engine + real connectors (Phases 4-5)
        services.AddScoped<ITicketSyncService, TicketSyncService>();
        services.AddHttpClient();
        services.AddScoped<IConnectorFactory, AutotaskConnectorFactory>();
        services.AddScoped<IConnectorFactory, ConnectWiseConnectorFactory>();

        // Optional SSRF egress guard on connector HttpClients (blocks private/reserved hosts).
        if (config.GetValue("Connectors:BlockPrivateEgress", false))
        {
            var allowed = new HashSet<string>(
                config.GetSection("Connectors:AllowedHosts").Get<string[]>() ?? [],
                StringComparer.OrdinalIgnoreCase);
            services.AddTransient(_ => new EgressGuard(allowed));
            services.AddHttpClient("autotask").AddHttpMessageHandler(sp => sp.GetRequiredService<EgressGuard>());
            services.AddHttpClient("connectwise").AddHttpMessageHandler(sp => sp.GetRequiredService<EgressGuard>());
        }

        // Client portal (Phase 6)
        services.AddScoped<IClientAccessResolver, ClientAccessResolver>();
        services.AddScoped<ITicketReadService, TicketReadService>();
        services.AddScoped<ITicketCommandService, TicketCommandService>();

        // Analytics (Phase 7)
        services.AddSingleton<IProductivityScorer, ProductivityScorer>();
        services.AddScoped<ITechnicianMetricsService, TechnicianMetricsService>();

        // Attachments (validate -> scan -> quarantine/store -> signed URL)
        services.AddSingleton(new AttachmentStorageOptions
        {
            SigningKey = config["Attachments:SigningKey"] ?? "dev-attachment-signing-key",
            PublicBaseUrl = config["Attachments:PublicBaseUrl"] ?? "http://localhost:5080",
        });
        services.AddSingleton(new AttachmentPolicy());
        services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();
        services.AddSingleton<IMalwareScanner, HeuristicMalwareScanner>();
        services.AddScoped<IAttachmentService, AttachmentService>();

        // Administration (Phase 8)
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IConnectionAdminService, ConnectionAdminService>();
        services.AddScoped<IMappingAdminService, MappingAdminService>();
        services.AddScoped<IJobMonitorService, JobMonitorService>();
        services.AddScoped<IIntegrationHealthService, IntegrationHealthService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

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
