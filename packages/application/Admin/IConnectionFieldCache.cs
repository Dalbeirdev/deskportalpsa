namespace Desk.Application.Admin;

/// <summary>
/// Caches a connection's discovered field options (boards, statuses, priorities, categories, work
/// types/roles) so they are fetched from the PSA at configure time (create / test / explicit
/// refresh) and reused everywhere else — rather than hitting the provider on every dropdown render.
/// </summary>
public interface IConnectionFieldCache
{
    ConnectionFieldsDto? Get(Guid connectionId);
    void Set(Guid connectionId, ConnectionFieldsDto fields);
    void Remove(Guid connectionId);
}
