using Desk.Domain.Common;

namespace Desk.Domain.Authorization;

/// <summary>
/// Actions a user may take on tickets within a board they have access to. A board is not its own
/// entity — it is the <c>QueueOrBoard</c> string Autotask/ConnectWise already sync onto every
/// ticket (see <see cref="Tickets.Ticket.QueueOrBoard"/>), scoped to a connection so the same
/// board name in two different PSAs is never conflated.
/// </summary>
[Flags]
public enum BoardAction
{
    View = 1,
    Create = 2,
    Edit = 4,
    Assign = 8,
    Close = 16,
    Delete = 32,
    Manage = 64,
}

public enum BoardAccessMode
{
    /// <summary>Fail-open default: every existing user keeps seeing every board until an admin
    /// deliberately narrows it — enforcement must never silently take access away the moment it
    /// ships, only when someone chooses to restrict it.</summary>
    All = 0,
    Selected = 1,
    None = 2,
}

/// <summary>One row per user: the overall board-access mode. Absence of a row means the same as
/// <see cref="BoardAccessMode.All"/> — the fail-open default applies even before this row exists.</summary>
public class UserBoardAccess : TenantEntity
{
    public Guid AppUserId { get; set; }
    public BoardAccessMode Mode { get; set; } = BoardAccessMode.All;
}

/// <summary>A specific board grant, populated only when the user's <see cref="UserBoardAccess.Mode"/>
/// is <see cref="BoardAccessMode.Selected"/>.</summary>
public class UserBoardGrant : TenantEntity
{
    public Guid AppUserId { get; set; }

    public Guid PsaConnectionId { get; set; }
    public Tenancy.PsaConnection? PsaConnection { get; set; }

    /// <summary>The literal <c>QueueOrBoard</c> value from that connection's synced tickets.</summary>
    public required string BoardName { get; set; }

    public BoardAction Actions { get; set; }
}
