using System.Collections.Concurrent;
using Desk.Application.Abstractions;

namespace Desk.Infrastructure.Secrets;

/// <summary>
/// Non-persistent secret store for local development and tests only. Registered when Vault is
/// not configured. NEVER selected in production (startup guards against it).
/// </summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _store = new();

    public Task<string> WriteAsync(string logicalName, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var reference = $"mem://{logicalName}/{Guid.NewGuid():N}";
        _store[reference] = data;
        return Task.FromResult(reference);
    }

    public Task<IReadOnlyDictionary<string, string>> ReadAsync(string secretRef, CancellationToken ct = default)
        => _store.TryGetValue(secretRef, out var v)
            ? Task.FromResult(v)
            : throw new KeyNotFoundException("Secret reference not found.");

    public Task RotateAsync(string secretRef, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        _store[secretRef] = data;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string secretRef, CancellationToken ct = default)
    {
        _store.TryRemove(secretRef, out _);
        return Task.CompletedTask;
    }
}
