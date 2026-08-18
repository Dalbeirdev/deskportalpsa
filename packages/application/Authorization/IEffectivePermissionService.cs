using Desk.Domain.Authorization;

namespace Desk.Application.Authorization;

/// <summary>Where an effective permission's scope actually came from — the thing an admin needs to
/// see to answer "why can this user do that", not just the answer itself.</summary>
public enum PermissionSource
{
    /// <summary>No role held this key at all.</summary>
    NoGrant,
    /// <summary>The (most permissive) scope came from one or more of the user's roles.</summary>
    RoleGrant,
    /// <summary>A user-level override replaced the role-derived scope with a wider one.</summary>
    OverrideGrant,
    /// <summary>A user-level override denies this permission outright, regardless of role.</summary>
    OverrideDeny,
}

/// <summary>A resolved board grant, scoped to the connection whose boards it names.</summary>
public sealed record BoardGrant(Guid PsaConnectionId, string BoardName, BoardAction Actions);

/// <summary>
/// The final answer to "how far can this user reach with this permission", after combining their
/// roles' grants with any personal override. Board access is resolved separately and kept alongside
/// rather than folded into <see cref="Scope"/>: the two are independent fences (which boards at all,
/// vs how far within them) and a caller applying this must AND them, never merge them into one value.
/// </summary>
public sealed record EffectivePermission(
    string PermissionKey,
    PermissionScope Scope,
    PermissionSource Source,
    BoardAccessMode BoardMode,
    IReadOnlyList<BoardGrant> BoardGrants)
{
    public bool IsDenied => Scope == PermissionScope.None;
}

/// <summary>
/// Resolves what a user can actually do with one permission, right now — the engine every
/// scope-sensitive query and every fine-grained enforcement check goes through.
///
/// Computed fresh per call, never cached across requests: the whole point of resolving permissions
/// from the database on every check (rather than baking them into a long-lived token) is that access
/// can change, or a user can be deactivated, without waiting for a token to expire. A cross-request
/// cache would quietly reintroduce that stale-access window.
/// </summary>
public interface IEffectivePermissionService
{
    Task<EffectivePermission> ResolveAsync(Guid appUserId, string permissionKey, CancellationToken ct = default);
}
