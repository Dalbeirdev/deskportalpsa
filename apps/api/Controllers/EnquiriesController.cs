using Desk.Api.Auth;
using Desk.Application.Marketing;
using Desk.Domain.Authorization;
using Desk.Domain.Marketing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Desk.Api.Controllers;

/// <summary>
/// No validation attributes: on a positional record they land on the generated property, and MVC
/// throws rather than binding — a 500 on the one endpoint anonymous visitors touch. Required-ness
/// is enforced in the service, which owns the rules and returns a message worth reading.
/// </summary>
public sealed record PublicEnquiryRequest(
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string Message,
    string? PreferredTime,
    string? SourcePage,
    string? Website);

/// <summary>
/// The public site's only way in. Anonymous by necessity, so it is deliberately narrow: it writes
/// an enquiry and returns nothing but success, never reading or echoing anything.
/// </summary>
[ApiController]
[Route("api/public/enquiries")]
[AllowAnonymous]
[EnableRateLimiting("public-forms")]
public sealed class PublicEnquiriesController(IEnquiryService enquiries) : ControllerBase
{
    [HttpPost("contact")]
    public Task<IActionResult> Contact([FromBody] PublicEnquiryRequest body, CancellationToken ct) =>
        SubmitAsync(EnquiryKind.Contact, body, ct);

    [HttpPost("meeting")]
    public Task<IActionResult> Meeting([FromBody] PublicEnquiryRequest body, CancellationToken ct) =>
        SubmitAsync(EnquiryKind.Meeting, body, ct);

    private async Task<IActionResult> SubmitAsync(EnquiryKind kind, PublicEnquiryRequest body, CancellationToken ct)
    {
        var ok = await enquiries.SubmitAsync(new SubmitEnquiryInput(
            kind, body.Name, body.Email, body.Company, body.Phone,
            body.Message, body.PreferredTime, body.SourcePage, body.Website), ct);

        // The only detail worth returning: whether the fields were usable. Anything more would let
        // an anonymous caller probe what is stored.
        return ok ? Accepted(new { received = true })
                  : BadRequest(new { error = "Please check your name, email address and message." });
    }
}

[ApiController]
[Route("api/admin/enquiries")]
[Authorize]
public sealed class AdminEnquiriesController(IEnquiryService enquiries) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.EnquiriesView)]
    public async Task<IActionResult> List([FromQuery] EnquiryStatus? status, CancellationToken ct) =>
        Ok(await enquiries.ListAsync(status, ct));

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Permissions.EnquiriesView)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetEnquiryStatusRequest body, CancellationToken ct) =>
        await enquiries.SetStatusAsync(id, body.Status, ct) ? NoContent() : NotFound();
}

public sealed record SetEnquiryStatusRequest(EnquiryStatus Status);
