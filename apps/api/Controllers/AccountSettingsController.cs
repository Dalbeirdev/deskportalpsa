using Desk.Application.Common;
using Desk.Application.ControlPanel;
using Desk.Application.Abstractions;
using Desk.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Desk.Api.Controllers;

/// <summary>
/// Client control panel — per-account settings (CP-2): approvers, escalation, business hours,
/// holidays and devices. Resolves the caller's client identity; the service enforces per-section
/// access and audits mutations.
/// </summary>
[ApiController]
[Route("api/control-panel")]
[Authorize]
public sealed class AccountSettingsController(
    ICurrentUser user,
    IClientAccessResolver accessResolver,
    IAccountSettingsService svc) : ControllerBase
{
    [HttpGet("account")]
    public async Task<IActionResult> Account(CancellationToken ct)
        => Ok(await svc.GetAccountAsync(await AccessAsync(ct), ct));

    // ---- Approvers ----
    [HttpGet("approvers")]
    public async Task<IActionResult> ListApprovers(CancellationToken ct)
        => Ok(await svc.ListApproversAsync(await AccessAsync(ct), ct));

    [HttpPut("approvers")]
    public async Task<IActionResult> SaveApprover([FromBody] ApproverInput input, CancellationToken ct)
        => Ok(await svc.SaveApproverAsync(await AccessAsync(ct), input, ct));

    [HttpDelete("approvers/{id:guid}")]
    public async Task<IActionResult> DeleteApprover(Guid id, CancellationToken ct)
    { await svc.DeleteApproverAsync(await AccessAsync(ct), id, ct); return NoContent(); }

    // ---- Escalation ----
    [HttpGet("escalation")]
    public async Task<IActionResult> ListEscalation(CancellationToken ct)
        => Ok(await svc.ListEscalationAsync(await AccessAsync(ct), ct));

    [HttpPut("escalation")]
    public async Task<IActionResult> SaveEscalation([FromBody] EscalationLevelInput input, CancellationToken ct)
        => Ok(await svc.SaveEscalationAsync(await AccessAsync(ct), input, ct));

    [HttpDelete("escalation/{id:guid}")]
    public async Task<IActionResult> DeleteEscalation(Guid id, CancellationToken ct)
    { await svc.DeleteEscalationAsync(await AccessAsync(ct), id, ct); return NoContent(); }

    // ---- Holidays ----
    [HttpGet("holidays")]
    public async Task<IActionResult> ListHolidays(CancellationToken ct)
        => Ok(await svc.ListHolidaysAsync(await AccessAsync(ct), ct));

    [HttpPut("holidays")]
    public async Task<IActionResult> SaveHoliday([FromBody] HolidayInput input, CancellationToken ct)
        => Ok(await svc.SaveHolidayAsync(await AccessAsync(ct), input, ct));

    [HttpDelete("holidays/{id:guid}")]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct)
    { await svc.DeleteHolidayAsync(await AccessAsync(ct), id, ct); return NoContent(); }

    // ---- Devices ----
    [HttpGet("devices")]
    public async Task<IActionResult> ListDevices(CancellationToken ct)
        => Ok(await svc.ListDevicesAsync(await AccessAsync(ct), ct));

    [HttpPut("devices")]
    public async Task<IActionResult> SaveDevice([FromBody] DeviceInput input, CancellationToken ct)
        => Ok(await svc.SaveDeviceAsync(await AccessAsync(ct), input, ct));

    [HttpDelete("devices/{id:guid}")]
    public async Task<IActionResult> DeleteDevice(Guid id, CancellationToken ct)
    { await svc.DeleteDeviceAsync(await AccessAsync(ct), id, ct); return NoContent(); }

    // ---- Business hours ----
    [HttpGet("business-hours")]
    public async Task<IActionResult> GetBusinessHours(CancellationToken ct)
        => Ok(await svc.GetBusinessHoursAsync(await AccessAsync(ct), ct));

    [HttpPut("business-hours")]
    public async Task<IActionResult> SaveBusinessHours([FromBody] BusinessHoursInput input, CancellationToken ct)
        => Ok(await svc.SaveBusinessHoursAsync(await AccessAsync(ct), input, ct));

    private async Task<ClientAccess> AccessAsync(CancellationToken ct)
        => await accessResolver.ResolveAsync(user.Subject ?? "", ct)
           ?? throw new ForbiddenException("The control panel is for client portal users.");
}
