namespace Desk.Application.Abstractions;

/// <summary>The authenticated caller for the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? Subject { get; }
    string? Email { get; }
    string? DisplayName { get; }
    Guid? OrganizationId { get; }

    /// <summary>The caller's AppUser row id, for staff. Null for client-portal users and for
    /// requests whose subject matched no staff account.</summary>
    Guid? UserId { get; }

    /// <summary>The caller's identifier in the external PSA, when they are a technician. Needed to
    /// scope anything to "this person's own work" — tickets and time entries reference the PSA-side
    /// id, not the portal user id.</summary>
    string? TechnicianExternalId { get; }

    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permissionKey);
}
