using Desk.Domain.Enums;
using Desk.PsaCore.Contracts;

namespace Desk.Connectors.Mock;

/// <summary>Factory for the mock connector. Real factories resolve credentials from Vault per connection.</summary>
public sealed class MockConnectorFactory(MockConnectorOptions options, TimeProvider clock) : IConnectorFactory
{
    public ProviderType Provider => options.Provider;

    public Task<IServiceManagementConnector> CreateAsync(Guid psaConnectionId, CancellationToken ct = default)
        => Task.FromResult<IServiceManagementConnector>(new MockConnector(options, clock));
}
