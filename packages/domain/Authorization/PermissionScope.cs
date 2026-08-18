using Desk.Domain.Common;

namespace Desk.Domain.Authorization;

/// <summary>
/// How far a granted permission reaches. Ranked loosely from broadest to narrowest — a later
/// enforcement phase resolves the most-permissive scope across a user's roles for a given
/// permission, then lets a user-level override replace (not merge with) that result outright.
/// </summary>
public enum PermissionScope
{
    All = 0,
    Department = 10,
    Team = 20,
    Assigned = 30,
    Own = 40,
    Selected = 50,
    None = 60,
}

public enum PermissionEffect
{
    Grant = 0,
    Deny = 1,
}

/// <summary>
/// A per-user exception to their role-derived permissions — e.g. a Technician role that denies
/// exporting tickets, overridden for one specific person who is explicitly allowed to. The row's
/// existence at a given (user, permission key) pair IS the override; there is at most one per pair.
///
/// Deliberately REPLACES the role-derived scope rather than merging with it, so "why does this user
/// have Department access to X" always has one complete, self-contained answer instead of requiring
/// both the role and the override to be read together.
/// </summary>
public class UserPermissionOverride : TenantEntity
{
    public Guid AppUserId { get; set; }

    /// <summary>A value from <see cref="Permissions"/>.</summary>
    public required string PermissionKey { get; set; }

    public PermissionEffect Effect { get; set; }

    /// <summary>Only meaningful when <see cref="Effect"/> is <see cref="PermissionEffect.Grant"/>.</summary>
    public PermissionScope? Scope { get; set; }

    /// <summary>Free-text note an admin leaves explaining why this exception exists.</summary>
    public string? Reason { get; set; }

    /// <summary>Set when this row was materialized by applying a permission template, so its
    /// provenance is distinguishable from a hand-authored override. Null for hand-authored rows.</summary>
    public Guid? AppliedFromTemplateId { get; set; }
}
