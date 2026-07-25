using Desk.Application.Jobs;
using Desk.Connectors.Mock;
using Desk.Domain.Enums;
using Desk.Infrastructure;
using Desk.PsaCore.Contracts;
using Desk.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

builder.Services.AddDeskInfrastructure(builder.Configuration);

// Job handlers
builder.Services.AddScoped<IJobHandler, InboundEventJobHandler>();

// Connectors — the real Autotask factory comes from AddDeskInfrastructure. ConnectWise has no
// real connector yet (Phase 5), so a mock stands in for it.
builder.Services.AddSingleton<IConnectorFactory>(sp =>
    new MockConnectorFactory(new MockConnectorOptions { Provider = ProviderType.ConnectWisePsa },
        sp.GetRequiredService<TimeProvider>()));

// Hosted services
builder.Services.AddHostedService<BackgroundJobPollingService>();
builder.Services.AddHostedService<PollingSyncService>();

var host = builder.Build();
host.Run();
