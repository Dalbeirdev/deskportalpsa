namespace Desk.Application.Abstractions;

/// <summary>The authenticated caller for the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? Subject { get; }
    string? Email { get; }
    string? DisplayName { get; }
    Guid? OrganizationId { get; }
    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permissionKey);
}
