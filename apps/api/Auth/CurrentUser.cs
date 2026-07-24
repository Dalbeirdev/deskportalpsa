using System.Security.Claims;
using Desk.Application.Abstractions;

namespace Desk.Api.Auth;

/// <summary>Reads the authenticated caller from the current HTTP request's claims principal.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string OrgClaim = "desk_org";
    public const string PermissionClaim = "desk_perm";
    public const string PlatformScopeClaim = "desk_platform";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public string? Subject => Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue("email");
    public string? DisplayName => Principal?.FindFirstValue("name") ?? Email;

    public Guid? OrganizationId =>
        Guid.TryParse(Principal?.FindFirstValue(OrgClaim), out var id) ? id : null;

    public IReadOnlySet<string> Permissions =>
        Principal?.FindAll(PermissionClaim).Select(c => c.Value).ToHashSet() ?? new HashSet<string>();

    public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
}
