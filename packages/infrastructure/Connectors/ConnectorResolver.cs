using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Connectors;

/// <summary>
/// Selects the registered <see cref="IConnectorFactory"/> for a connection's provider and builds a
/// connector. The connection lookup runs under the caller's tenant scope, so a connection from
/// another tenant is simply not found.
/// </summary>
public sealed class ConnectorResolver(DeskDbContext db, IEnumerable<IConnectorFactory> factories) : IConnectorResolver
{
    private readonly IReadOnlyDictionary<ProviderType, IConnectorFactory> _factories =
        factories.ToDictionary(f => f.Provider);

    public async Task<IServiceManagementConnector> ResolveAsync(Guid psaConnectionId, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == psaConnectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        if (!connection.IsEnabled)
            throw new ValidationFailedException($"PSA connection '{connection.Name}' is disabled.");

        if (!_factories.TryGetValue(connection.Provider, out var factory))
            throw new ValidationFailedException($"No connector registered for provider {connection.Provider}.");

        return await factory.CreateAsync(psaConnectionId, ct);
    }
}
