using Desk.Application.Common;
using Desk.Application.ControlPanel;
using Desk.Application.Abstractions;
using Desk.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Api.Controllers;

/// <summary>
/// Client control panel — CP-3 content: announcements, branding and the account report. Resolves the
/// caller's client identity; the service enforces per-section access and audits mutations.
/// </summary>
[ApiController]
[Route("api/control-panel")]
[Authorize]
public sealed class ClientContentController(
    ICurrentUser user,
    IClientAccessResolver accessResolver,
    IClientContentService svc) : ControllerBase
{
    [HttpGet("announcements")]
    public async Task<IActionResult> ListAnnouncements(CancellationToken ct)
        => Ok(await svc.ListAnnouncementsAsync(await AccessAsync(ct), ct));

    [HttpPut("announcements")]
    public async Task<IActionResult> SaveAnnouncement([FromBody] AnnouncementInput input, CancellationToken ct)
        => Ok(await svc.SaveAnnouncementAsync(await AccessAsync(ct), input, ct));

    [HttpDelete("announcements/{id:guid}")]
    public async Task<IActionResult> DeleteAnnouncement(Guid id, CancellationToken ct)
    { await svc.DeleteAnnouncementAsync(await AccessAsync(ct), id, ct); return NoContent(); }

    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
        => Ok(await svc.GetBrandingAsync(await AccessAsync(ct), ct));

    [HttpPut("branding")]
    public async Task<IActionResult> SaveBranding([FromBody] BrandingInput input, CancellationToken ct)
        => Ok(await svc.SaveBrandingAsync(await AccessAsync(ct), input, ct));

    [HttpGet("report")]
    public async Task<IActionResult> Report(CancellationToken ct)
        => Ok(await svc.GetReportAsync(await AccessAsync(ct), ct));

    [HttpGet("faq")]
    public async Task<IActionResult> ListFaq(CancellationToken ct)
        => Ok(await svc.ListFaqAsync(await AccessAsync(ct), ct));

    [HttpPut("faq")]
    public async Task<IActionResult> SaveFaq([FromBody] FaqArticleInput input, CancellationToken ct)
        => Ok(await svc.SaveFaqAsync(await AccessAsync(ct), input, ct));

    [HttpDelete("faq/{id:guid}")]
    public async Task<IActionResult> DeleteFaq(Guid id, CancellationToken ct)
    { await svc.DeleteFaqAsync(await AccessAsync(ct), id, ct); return NoContent(); }

    private async Task<ClientAccess> AccessAsync(CancellationToken ct)
        => await accessResolver.ResolveAsync(user.Subject ?? "", ct)
           ?? throw new ForbiddenException("The control panel is for client portal users.");
}
