using Desk.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Api.Controllers;

/// <summary>Echoes the resolved identity for the current token — a minimal authenticated probe.</summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(ICurrentUser user, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        subject = user.Subject,
        email = user.Email,
        displayName = user.DisplayName,
        organizationId = tenant.OrganizationId,
        isPlatformScope = tenant.IsPlatformScope,
        permissions = user.Permissions.OrderBy(p => p),
    });
}
