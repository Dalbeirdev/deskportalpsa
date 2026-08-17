using Desk.Application.Admin;
using Desk.Application.Sync;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Api.Auth;
using Desk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Controllers;

/// <summary>PSA connection administration. Secrets are written to the encrypted store and never returned.</summary>
[ApiController]
[Route("api/admin/connections")]
public sealed class AdminConnectionsController(
    IConnectionAdminService svc,
    IConnectionSyncRunner syncRunner,
    DeskDbContext db,
    IConfiguration config) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.ConnectionsView)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await svc.ListAsync(ct));

    /// <summary>Uploads a logo for the connections page. Small by design — this is a brand mark.</summary>
    [HttpPost("{id:guid}/logo")]
    [RequirePermission(Permissions.ConnectionsManage)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Choose an image to upload." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return Ok(await svc.UploadLogoAsync(id, new ConnectionLogoUpload(file.FileName, file.ContentType, ms.ToArray()), ct));
    }

    [HttpDelete("{id:guid}/logo")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> RemoveLogo(Guid id, CancellationToken ct)
    {
        await svc.RemoveLogoAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Serves the stored logo. nosniff and a locked-down CSP because this route returns bytes an
    /// administrator supplied — the browser must never be talked into treating them as a document.
    /// </summary>
    [HttpGet("{id:guid}/logo")]
    [RequirePermission(Permissions.ConnectionsView)]
    public async Task<IActionResult> GetLogo(Guid id, CancellationToken ct)
    {
        var logo = await svc.GetLogoAsync(id, ct);
        if (logo is null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return File(logo.Content, logo.ContentType);
    }

    [HttpPost]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> Create([FromBody] CreateConnectionInput input, CancellationToken ct)
        => Ok(await svc.CreateAsync(input, ct));

    [HttpPost("{id:guid}/enabled")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> SetEnabled(Guid id, [FromBody] bool enabled, CancellationToken ct)
    {
        await svc.SetEnabledAsync(id, enabled, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct)
        => Ok(await svc.TestAsync(id, ct));

    /// <summary>
    /// Pull tickets from the provider into the portal. Incremental by default; pass full=true to
    /// re-pull everything (e.g. after changing field mappings, so existing tickets are re-translated).
    /// </summary>
    [HttpPost("{id:guid}/sync")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> Sync(Guid id, [FromQuery] bool full, CancellationToken ct)
    {
        var result = await syncRunner.RunAsync(id, full, ct);
        await EnsureLocalClientIdentityAsync(id, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConnectionInput input, CancellationToken ct)
        => Ok(await svc.UpdateAsync(id, input, ct));

    [HttpGet("{id:guid}/fields")]
    [RequirePermission(Permissions.ConnectionsView)]
    public async Task<IActionResult> Fields(Guid id, CancellationToken ct)
        => Ok(await svc.GetFieldsAsync(id, ct));

    /// <summary>Sync behaviour + import filters for this connection.</summary>
    [HttpGet("{id:guid}/settings")]
    [RequirePermission(Permissions.ConnectionsView)]
    public async Task<IActionResult> GetSettings(Guid id, CancellationToken ct)
        => Ok(await svc.GetSettingsAsync(id, ct));

    [HttpPut("{id:guid}/settings")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> SaveSettings(Guid id, [FromBody] ConnectionSettingsDto input, CancellationToken ct)
        => Ok(await svc.SaveSettingsAsync(id, input, ct));

    /// <summary>Force a fresh discovery of field options from the PSA and update the cache.</summary>
    [HttpPost("{id:guid}/fields/refresh")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> RefreshFields(Guid id, CancellationToken ct)
        => Ok(await svc.RefreshFieldsAsync(id, ct));

    // Local demo only: the dev auto-login is an MSP admin, not a client. The client-portal pages
    // (Tickets, Notifications, Profile) resolve by client identity, so once a sync has produced real
    // tickets we link the dev subject to the busiest synced company (as its administrator) — using
    // real synced data, never fabricated rows — so the whole portal shows live data under one login.
    private async Task EnsureLocalClientIdentityAsync(Guid connectionId, CancellationToken ct)
    {
        if (!config.GetValue("LocalMode:Enabled", false)) return;
        if (await db.ClientUsers.IgnoreQueryFilters().AnyAsync(u => u.IdpSubject == DatabaseSeeder.DevAdminSubject, ct)) return;

        var companyId = await db.Tickets
            .Where(t => t.PsaConnectionId == connectionId)
            .GroupBy(t => t.ClientCompanyId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);
        if (companyId == Guid.Empty) return; // no tickets synced yet — nothing to attach to

        var company = await db.ClientCompanies.FirstAsync(c => c.Id == companyId, ct);
        db.ClientUsers.Add(new ClientUser
        {
            MspOrganizationId = company.MspOrganizationId,
            ClientCompanyId = companyId,
            Email = "dev-admin@local",
            DisplayName = "Demo Admin",
            IdpSubject = DatabaseSeeder.DevAdminSubject,
            IsCompanyAdministrator = true,
            IsActive = true,
        });
        await db.SaveChangesAsync(ct);
    }
}


/// <summary>Field-mapping administration with versioning + rollback (all audited).</summary>
[ApiController]
[Route("api/admin/mappings")]
public sealed class AdminMappingsController(IMappingAdminService svc) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.MappingsView)]
    public async Task<IActionResult> List([FromQuery] ProviderType provider, CancellationToken ct)
        => Ok(await svc.ListAsync(provider, ct));

    [HttpPost]
    [RequirePermission(Permissions.MappingsManage)]
    public async Task<IActionResult> Upsert([FromBody] UpsertMappingInput input, [FromQuery] string? note, CancellationToken ct)
        => Ok(await svc.UpsertAsync(input, note, ct));

    [HttpDelete("{ruleId:guid}")]
    [RequirePermission(Permissions.MappingsManage)]
    public async Task<IActionResult> Delete(Guid ruleId, CancellationToken ct)
    { await svc.DeleteAsync(ruleId, ct); return NoContent(); }

    [HttpGet("versions")]
    [RequirePermission(Permissions.MappingsView)]
    public async Task<IActionResult> Versions([FromQuery] ProviderType provider, [FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await svc.VersionsAsync(provider, connectionId, ct));

    [HttpPost("versions/{versionId:guid}/rollback")]
    [RequirePermission(Permissions.MappingsManage)]
    public async Task<IActionResult> Rollback(Guid versionId, CancellationToken ct)
    {
        await svc.RollbackAsync(versionId, ct);
        return NoContent();
    }
}

/// <summary>Background job monitor with dead-letter reprocessing.</summary>
[ApiController]
[Route("api/admin/jobs")]
public sealed class JobMonitorController(IJobMonitorService svc) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.JobsManage)]
    public async Task<IActionResult> List([FromQuery] BackgroundJobStatus? status, CancellationToken ct)
        => Ok(await svc.ListAsync(status, ct));

    [HttpPost("{id:guid}/reprocess")]
    [RequirePermission(Permissions.JobsManage)]
    public async Task<IActionResult> Reprocess(Guid id, CancellationToken ct)
    {
        await svc.ReprocessAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin")]
public sealed class AdminReadController(
    IIntegrationHealthService health,
    IAuditQueryService auditQuery,
    ITicketResyncService resync,
    IUserAdminService users) : ControllerBase
{
    /// <summary>Tickets the portal holds that never reached the PSA — the count and which they are.</summary>
    [HttpGet("tickets/unsynced")]
    [RequirePermission(Permissions.IntegrationHealthView)]
    public async Task<IActionResult> Unsynced([FromQuery] Guid? connectionId, CancellationToken ct)
        => Ok(await resync.ListAsync(connectionId, ct));

    /// <summary>
    /// Pushes one outstanding ticket again. Deliberately one at a time: each retry hits the provider
    /// and can fail for its own reason, and a bulk button would bury which ones did.
    /// </summary>
    [HttpPost("tickets/{id:guid}/resync")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> Resync(Guid id, CancellationToken ct)
        => Ok(await resync.ResyncAsync(id, ct));

    [HttpGet("health")]
    [RequirePermission(Permissions.IntegrationHealthView)]
    public async Task<IActionResult> Health(CancellationToken ct) => Ok(await health.SnapshotAsync(ct));

    [HttpGet("audit")]
    [RequirePermission(Permissions.AuditView)]
    public async Task<IActionResult> Audit([FromQuery] string? action, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await auditQuery.ListAsync(take, action, ct));

    /// <summary>
    /// Real attachment-storage usage. The sidebar used to show a hardcoded "6.8 GB of 10 GB" —
    /// decoration presented as fact. Only CLEAN files count: quarantined uploads keep no bytes.
    /// </summary>
    [HttpGet("storage")]
    [RequirePermission(Permissions.IntegrationHealthView)]
    public async Task<IActionResult> Storage([FromServices] DeskDbContext db, CancellationToken ct)
    {
        var clean = db.TicketAttachments.Where(a => a.ScanStatus == Desk.Domain.Enums.AttachmentScanStatus.Clean);
        return Ok(new
        {
            usedBytes = await clean.SumAsync(a => (long?)a.SizeBytes, ct) ?? 0L,
            fileCount = await clean.CountAsync(ct),
            ticketCount = await clean.Select(a => a.TicketId).Distinct().CountAsync(ct),
        });
    }

    [HttpGet("users")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await users.ListAsync(ct));

    [HttpGet("roles")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> StaffRoles(CancellationToken ct) => Ok(await users.StaffRolesAsync(ct));

    [HttpPost("users")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> CreateUser([FromBody] CreateStaffUserInput input, CancellationToken ct)
        => Ok(await users.CreateAsync(input, ct));

    [HttpPut("users/{id:guid}/active")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> SetUserActive(Guid id, [FromBody] bool active, CancellationToken ct)
    { await users.SetActiveAsync(id, active, ct); return NoContent(); }

    [HttpPost("users/{id:guid}/roles/{roleId:guid}")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> AssignRole(Guid id, Guid roleId, CancellationToken ct)
    { await users.AssignRoleAsync(id, roleId, ct); return NoContent(); }

    [HttpDelete("users/{id:guid}/roles/{roleId:guid}")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId, CancellationToken ct)
    { await users.RemoveRoleAsync(id, roleId, ct); return NoContent(); }
}
