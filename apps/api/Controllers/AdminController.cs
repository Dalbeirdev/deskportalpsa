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

/// <summary>PSA connection administration. Secrets are written to Vault and never returned.</summary>
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

    /// <summary>Pull tickets from the provider into the portal (manual "sync now").</summary>
    [HttpPost("{id:guid}/sync")]
    [RequirePermission(Permissions.ConnectionsManage)]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct)
    {
        var result = await syncRunner.RunAsync(id, ct);
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
    IUserAdminService users) : ControllerBase
{
    [HttpGet("health")]
    [RequirePermission(Permissions.IntegrationHealthView)]
    public async Task<IActionResult> Health(CancellationToken ct) => Ok(await health.SnapshotAsync(ct));

    [HttpGet("audit")]
    [RequirePermission(Permissions.AuditView)]
    public async Task<IActionResult> Audit([FromQuery] string? action, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await auditQuery.ListAsync(take, action, ct));

    [HttpGet("users")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await users.ListAsync(ct));
}
