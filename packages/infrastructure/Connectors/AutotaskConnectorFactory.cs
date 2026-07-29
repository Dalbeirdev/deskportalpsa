using Desk.Application.Abstractions;
using Desk.Application.Common;
using Desk.Connectors.Autotask;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Connectors;

/// <summary>
/// Builds an <see cref="AutotaskConnector"/> for a connection: loads the connection row (endpoint),
/// resolves its credentials from the secret store by the stored reference, and hands the connector
/// a configured HttpClient. Raw secrets exist only for the lifetime of the built connector.
/// </summary>
public sealed class AutotaskConnectorFactory(
    DeskDbContext db,
    ISecretStore secrets,
    IHttpClientFactory httpFactory,
    TimeProvider clock) : IConnectorFactory
{
    public ProviderType Provider => ProviderType.AutotaskPsa;

    public async Task<IServiceManagementConnector> CreateAsync(Guid psaConnectionId, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == psaConnectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        var secret = await secrets.ReadAsync(connection.CredentialSecretRef, ct);

        var config = new AutotaskConnectorConfig
        {
            BaseUrl = EnsureTrailingSlash(connection.ApiEndpoint),
            Credentials = new AutotaskCredentials(
                ApiIntegrationCode: Require(secret, "ApiIntegrationCode"),
                UserName: Require(secret, "UserName"),
                Secret: Require(secret, "Secret")),
            WebhookSecret = secret.GetValueOrDefault("WebhookSecret", ""),
        };

        var http = httpFactory.CreateClient("autotask");
        http.BaseAddress = new Uri(config.BaseUrl);

        return new AutotaskConnector(http, config, clock);
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";

    private static string Require(IReadOnlyDictionary<string, string> secret, string key)
        => secret.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)
            ? v
            : throw new ValidationFailedException($"Autotask credential '{key}' missing from the secret store.");
}
