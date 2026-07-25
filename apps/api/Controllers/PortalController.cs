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

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var access = await AccessAsync(ct);
        return Ok(new
        {
            displayName = user.DisplayName,
            email = user.Email,
            clientCompanyId = access.ClientCompanyId,
            isCompanyAdministrator = access.IsCompanyAdministrator,
        });
    }

    private async Task<ClientAccess> AccessAsync(CancellationToken ct)
        => await accessResolver.ResolveAsync(user.Subject ?? "", ct)
           ?? throw new ForbiddenException("This endpoint is for client portal users.");
}
