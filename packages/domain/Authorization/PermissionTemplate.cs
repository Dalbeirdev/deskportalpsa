using Desk.Domain.Common;
using Desk.Domain.Enums;

namespace Desk.Domain.Authorization;

/// <summary>
/// A reusable starting set of permission grants an admin can apply when creating a user (Full
/// Administrator, Senior Technician, Dispatcher, …). Mirrors <see cref="Identity.Role"/>'s
/// nullable-org shape rather than being tenant-owned: the 8 built-ins are shared, admin-visible
/// starting points exactly like the 7 built-in roles, not per-tenant data.
///
/// Applying a template does not create a third grant mechanism — it materializes each entry as a
/// <see cref="UserPermissionOverride"/> row (tagged via <see cref="UserPermissionOverride.AppliedFromTemplateId"/>),
/// reusing the one override pathway a later phase's effective-permission calculation already has to
/// resolve, rather than adding a second path an admin would need to reason about separately.
/// </summary>
public class PermissionTemplate : BaseEntity, INullableTenantScoped
{
    /// <summary>Null for the 8 built-in templates, shared across every tenant.</summary>
    public Guid? MspOrganizationId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>The role a user is assigned when this template is picked on creation.</summary>
    public RoleType BaseRoleType { get; set; }

    public bool IsSystemTemplate { get; set; }

    public ICollection<PermissionTemplateEntry> Entries { get; set; } = new List<PermissionTemplateEntry>();
}

/// <summary>One permission grant/denial within a template.</summary>
public class PermissionTemplateEntry : BaseEntity
{
    public Guid PermissionTemplateId { get; set; }
    public PermissionTemplate? PermissionTemplate { get; set; }

    /// <summary>A value from <see cref="Permissions"/>.</summary>
    public required string PermissionKey { get; set; }

    public PermissionEffect Effect { get; set; }
    public PermissionScope? Scope { get; set; }
}
