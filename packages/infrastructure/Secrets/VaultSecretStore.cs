using Desk.Application.Abstractions;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines;

namespace Desk.Infrastructure.Secrets;

public sealed class VaultOptions
{
    public string Address { get; set; } = "http://localhost:8200";
    public string Token { get; set; } = "";
    public string MountPoint { get; set; } = "secret";
    public string PathPrefix { get; set; } = "desk/psa-credentials";
}

/// <summary>
/// HashiCorp Vault-backed secret store (KV v2). PSA credentials live only here; the database
/// stores the returned reference path. Values are never logged.
/// </summary>
public sealed class VaultSecretStore(IVaultClient client, VaultOptions options) : ISecretStore
{
    public async Task<string> WriteAsync(string logicalName, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var path = $"{options.PathPrefix}/{logicalName}/{Guid.NewGuid():N}";
        await client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path: path,
            data: data.ToDictionary(kv => kv.Key, object (kv) => kv.Value),
            mountPoint: options.MountPoint);
        return path;
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadAsync(string secretRef, CancellationToken ct = default)
    {
        var secret = await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(
            path: secretRef, mountPoint: options.MountPoint);
        return secret.Data.Data.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty);
    }

    public Task RotateAsync(string secretRef, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
        => client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            path: secretRef,
            data: data.ToDictionary(kv => kv.Key, object (kv) => kv.Value),
            mountPoint: options.MountPoint);

    public Task DeleteAsync(string secretRef, CancellationToken ct = default)
        => client.V1.Secrets.KeyValue.V2.DeleteMetadataAsync(secretRef, options.MountPoint);

    public static IVaultClient BuildClient(VaultOptions options)
    {
        var settings = new VaultClientSettings(options.Address, new TokenAuthMethodInfo(options.Token));
        return new VaultClient(settings);
    }
}
