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

// Connectors — Phase-3 placeholder mock factories for the Wave-1 providers (replaced in phases 4-5).
foreach (var provider in new[] { ProviderType.ConnectWisePsa, ProviderType.AutotaskPsa })
{
    builder.Services.AddSingleton<IConnectorFactory>(sp =>
        new MockConnectorFactory(new MockConnectorOptions { Provider = provider },
            sp.GetRequiredService<TimeProvider>()));
}

// Hosted services
builder.Services.AddHostedService<BackgroundJobPollingService>();
builder.Services.AddHostedService<PollingSyncService>();

var host = builder.Build();
host.Run();
