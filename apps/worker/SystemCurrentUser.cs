using Desk.Application.Abstractions;

namespace Desk.Worker;

/// <summary>
/// The worker's identity. ICurrentUser is HTTP-request-bound in the API; a background process has
/// no request, but the shared service graph (audit writing above all) still needs an answer to
/// "who is acting". The answer is: the system — with no permissions, so nothing in the worker can
/// accidentally pass a permission check that was written with a human in mind.
///
/// Without this registration the worker could not even START in Development: host builder DI
/// validation walks the whole graph, and every audit-writing service failed to construct.
/// </summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;
    public string? Subject => "system";
    public string? Email => null;
    public string? DisplayName => "System";
    public Guid? OrganizationId => null;
    public IReadOnlySet<string> Permissions => new HashSet<string>();
    public bool HasPermission(string permissionKey) => false;
}
