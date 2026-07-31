using Desk.Application.Tickets;

namespace Desk.Application.ControlPanel;

/// <summary>
/// Client-facing control panel: a client company administrator manages their own ticket service
/// instructions and delegates per-section (optionally per-account) access to their own users.
/// Every method is scoped to the caller's client identity (<see cref="ClientAccess"/>); write
/// operations additionally enforce the caller's effective access.
/// </summary>
public interface IControlPanelService
{
    /// <summary>Which control-panel sections the caller may use, plus their company context (drives the nav).</summary>
    Task<ControlPanelCapabilities> GetCapabilitiesAsync(ClientAccess access, CancellationToken ct = default);

    /// <summary>The organization-wide default instructions plus any per-account overrides the caller can see.</summary>
    Task<InstructionsView> GetInstructionsAsync(ClientAccess access, CancellationToken ct = default);

    /// <summary>Upsert the single instruction row for a scope (null company = the org-wide default).</summary>
    Task<InstructionDto> SaveInstructionAsync(ClientAccess access, Guid? clientCompanyId, string body, CancellationToken ct = default);

    /// <summary>All client portal users in the caller's company, with their access grants (admin only).</summary>
    Task<IReadOnlyList<ClientUserDto>> ListUsersAsync(ClientAccess access, CancellationToken ct = default);

    /// <summary>Invite (create) a client portal user in the caller's company (admin only).</summary>
    Task<ClientUserDto> InviteUserAsync(ClientAccess access, InviteClientUserInput input, CancellationToken ct = default);

    /// <summary>Enable/disable a client portal user (admin only). A company cannot disable its last admin.</summary>
    Task SetUserActiveAsync(ClientAccess access, Guid clientUserId, bool active, CancellationToken ct = default);

    /// <summary>Replace a user's section/account access grants, or toggle their administrator flag (admin only).</summary>
    Task<ClientUserDto> SetUserAccessAsync(ClientAccess access, Guid clientUserId, SetAccessInput input, CancellationToken ct = default);
}
