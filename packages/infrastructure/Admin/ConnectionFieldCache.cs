using System.Collections.Concurrent;
using Desk.Application.Admin;

namespace Desk.Infrastructure.Admin;

/// <summary>In-memory per-instance field cache. Populated at connection configure time; a refresh
/// re-discovers from the PSA. (A distributed/persisted cache can replace this without touching callers.)</summary>
public sealed class ConnectionFieldCache : IConnectionFieldCache
{
    private readonly ConcurrentDictionary<Guid, ConnectionFieldsDto> _cache = new();

    public ConnectionFieldsDto? Get(Guid connectionId) => _cache.TryGetValue(connectionId, out var f) ? f : null;
    public void Set(Guid connectionId, ConnectionFieldsDto fields) => _cache[connectionId] = fields;
    public void Remove(Guid connectionId) => _cache.TryRemove(connectionId, out _);
}
