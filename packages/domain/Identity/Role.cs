using Desk.Domain.Authorization;
using Desk.Domain.Common;
using Desk.Domain.Enums;

namespace Desk.Domain.Identity;

/// <summary>
/// A role groups a set of permission claims. The 7 built-in roles are seeded; MSP admins
/// may create additional custom roles scoped to their organization.
/// </summary>
public class Role : BaseEntity
{
    /// <summary>Null for the built-in platform roles that apply across all tenants.</summary>
    public Guid? MspOrganizationId { get; set; }

    public required string Name { get; set; }
    public RoleType? BuiltInType { get; set; }
    public bool IsSystemRole { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

/// <summary>Join between a role and a single permission-claim key.</summary>
public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }

    /// <summary>A value from <see cref="Authorization.Permissions"/>.</summary>
    public required string PermissionKey { get; set; }

    /// <summary>How far this grant reaches. Defaults to <see cref="PermissionScope.All"/> — the
    /// scope every existing role/permission pair effectively has today, since nothing enforces
    /// scope yet. Added ahead of enforcement so the migration itself is provably inert: this
    /// column exists and defaults correctly before anything reads it.</summary>
    public PermissionScope Scope { get; set; } = PermissionScope.All;
}

/// <summary>Assignment of a role to an internal user.</summary>
public class UserRole : BaseEntity
{
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }

    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}
