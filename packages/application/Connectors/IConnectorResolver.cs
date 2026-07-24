using Desk.PsaCore.Contracts;

namespace Desk.Application.Connectors;

/// <summary>
/// Resolves a ready-to-use connector for a PSA connection: looks up the connection (tenant-scoped),
/// selects the factory registered for its provider, and builds a connector bound to it. Adding a
/// provider means registering one more <see cref="IConnectorFactory"/> — no change here or in callers.
/// </summary>
public interface IConnectorResolver
{
    Task<IServiceManagementConnector> ResolveAsync(Guid psaConnectionId, CancellationToken ct = default);
}
