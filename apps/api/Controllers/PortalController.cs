using Desk.Api.Auth;
using Desk.Application.Abstractions;
using Desk.Application.Common;
using Desk.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Api.Controllers;

/// <summary>Client-portal notifications + profile. Both are scoped to the caller's client identity.</summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class PortalController(
    ICurrentUser user,
    IClientAccessResolver accessResolver,
    ITicketReadService reads) : ControllerBase
{
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct)
        => Ok(await reads.RecentActivityAsync(await AccessAsync(ct), 10, ct));

    /// <summary>Dated feed of what actually happened on the caller's visible tickets.</summary>
    [HttpGet("notifications/history")]
    public async Task<IActionResult> NotificationHistory(CancellationToken ct)
        => Ok(await reads.ActivityHistoryAsync(await AccessAsync(ct), 50, ct));

    /// <summary>
    /// The caller's own profile. Unlike everything else on this controller it is
    /// NOT client-scoped: staff (technicians, managers, MSP admins) have profiles
    /// too, and the old client-only resolution handed every one of them a 403 on
    /// their own profile page.
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> Profile([FromServices] IProfileService profiles, CancellationToken ct)
    {
        var dto = await profiles.GetAsync(user.Subject ?? "", ct);
        return dto is null
            ? throw new ForbiddenException("No active account is linked to this sign-in.")
            : Ok(dto);
    }

    /// <summary>
    /// Self-service edit: display name and contact email. Role is deliberately
    /// absent — self-editing a role is privilege escalation, so roles change only
    /// through admin user management. When sign-in is IdP-bound, the email edited
    /// here is the contact address; it does not change how the user logs in.
    /// </summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromServices] IProfileService profiles,
        [FromBody] UpdateProfileRequest body,
        CancellationToken ct)
        => Ok(await profiles.UpdateAsync(user.Subject ?? "", body.DisplayName, body.Email, ct));

    public sealed record UpdateProfileRequest(string DisplayName, string Email);

    private async Task<ClientAccess> AccessAsync(CancellationToken ct)
        => await accessResolver.ResolveAsync(user.Subject ?? "", ct)
           ?? throw new ForbiddenException("This endpoint is for client portal users.");
}
