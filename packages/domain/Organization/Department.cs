using Desk.Domain.Common;

namespace Desk.Domain.Organization;

/// <summary>
/// A staff organizational unit (IT Support, NOC, Billing, …). Tenant-owned and fully editable —
/// the 7 defaults seeded per tenant are a starting point, not a fixed catalogue.
///
/// Purely organizational at this phase: nothing yet reads this to scope a permission. That comes
/// in a later phase once <see cref="Authorization.PermissionScope"/> is actually consulted by
/// enforcement — this table exists now so department/team assignment can be built and used for
/// data entry ahead of that, without the assignment itself being a security-relevant change.
/// </summary>
public class Department : TenantEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>One of the 7 seeded defaults. Still editable/deletable — not a protected row.</summary>
    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<Team> Teams { get; set; } = new List<Team>();
}

/// <summary>A sub-group within a department (e.g. IT Support → Level 1 / Level 2 / Escalation).</summary>
public class Team : TenantEntity
{
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// A user's membership in a department. Exactly one row per user may have <see cref="IsPrimary"/>
/// set (enforced by a partial unique index) — every other department the user belongs to is
/// secondary.
/// </summary>
public class UserDepartment : TenantEntity
{
    public Guid AppUserId { get; set; }
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>A user's membership in a team. A user may belong to teams across different departments.</summary>
public class UserTeam : TenantEntity
{
    public Guid AppUserId { get; set; }
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
}
