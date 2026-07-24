using Desk.Domain.Common;

namespace Desk.Domain.Identity;

/// <summary>
/// An internal platform user (technician, manager, MSP admin, auditor, platform super-admin).
/// Client-side users are modelled separately as <see cref="Desk.Domain.Tenancy.ClientUser"/>.
///
/// Platform super-administrators are the only users allowed to be cross-tenant; all other
/// users are pinned to a single MSP organization.
/// </summary>
public class AppUser : BaseEntity
{
    /// <summary>Null only for platform super-administrators (cross-tenant).</summary>
    public Guid? MspOrganizationId { get; set; }

    public required string Email { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>Subject (sub) claim from the identity provider (Keycloak).</summary>
    public string? IdpSubject { get; set; }

    /// <summary>Identifier of the matching resource/member in the external PSA, if this user is a technician.</summary>
    public string? ExternalTechnicianId { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
}
