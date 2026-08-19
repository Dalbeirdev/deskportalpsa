using System.Text.Json;
using Desk.Application.Abstractions;

namespace Desk.Infrastructure.Secrets;

/// <summary>
/// File-backed secret store for LOCAL MODE ONLY — the persistent counterpart to the SQLite database,
/// so PSA credentials survive restarts. Credentials are written to a JSON file (gitignored). Never
/// selected in production (startup guards against it); production uses the Vault-backed store.
/// </summary>
public sealed class FileSecretStore : ISecretStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, string>> _store;

    public FileSecretStore(string path)
    {
        _path = path;
        _store = Load(path);
    }

    private static Dictionary<string, Dictionary<string, string>> Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)) ?? new();
        }
        catch { /* corrupt/unreadable → start empty */ }
        return new();
    }

    private void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_store));
        File.Move(tmp, _path, overwrite: true); // atomic-ish replace
    }

    public Task<string> WriteAsync(string logicalName, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var reference = $"file://{logicalName}/{Guid.NewGuid():N}";
        lock (_gate) { _store[reference] = new Dictionary<string, string>(data); Save(); }
        return Task.FromResult(reference);
    }

    public Task<IReadOnlyDictionary<string, string>> ReadAsync(string secretRef, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_store.TryGetValue(secretRef, out var v))
                return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(v));
        }
        throw new KeyNotFoundException("Secret reference not found.");
    }

    public Task<string> RotateAsync(string secretRef, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        // A dictionary key has no length limit, so the reference is always reusable here.
        lock (_gate) { _store[secretRef] = new Dictionary<string, string>(data); Save(); }
        return Task.FromResult(secretRef);
    }

    public Task DeleteAsync(string secretRef, CancellationToken ct = default)
    {
        lock (_gate) { if (_store.Remove(secretRef)) Save(); }
        return Task.CompletedTask;
    }
}
