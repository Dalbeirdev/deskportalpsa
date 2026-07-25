using Desk.Application.Abstractions;
using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Admin;

public sealed class JobMonitorService(DeskDbContext db, IAuditWriter audit, TimeProvider clock) : IJobMonitorService
{
    public async Task<IReadOnlyList<JobSummary>> ListAsync(BackgroundJobStatus? status, CancellationToken ct = default)
    {
        var q = db.BackgroundJobs.AsNoTracking().AsQueryable();
        if (status is { } s) q = q.Where(j => j.Status == s);
        return await q.OrderByDescending(j => j.CreatedAt).Take(200)
            .Select(j => new JobSummary(j.Id, j.JobType, j.Status, j.Attempts, j.MaxAttempts, j.NextAttemptAt, j.LastError, j.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task ReprocessAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await db.BackgroundJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
            ?? throw new NotFoundException("Background job");
        if (job.Status != BackgroundJobStatus.DeadLettered)
            throw new ValidationFailedException("Only dead-lettered jobs can be reprocessed.");

        job.Status = BackgroundJobStatus.Queued;
        job.Attempts = 0;
        job.NextAttemptAt = clock.GetUtcNow();
        job.LastError = null;
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("job.reprocessed", "BackgroundJob", jobId.ToString(), new { job.JobType }, ct);
    }
}

public sealed class IntegrationHealthService(DeskDbContext db) : IIntegrationHealthService
{
    public async Task<IReadOnlyList<ConnectionHealthDto>> SnapshotAsync(CancellationToken ct = default)
    {
        var connections = await db.PsaConnections.AsNoTracking().ToListAsync(ct);
        var result = new List<ConnectionHealthDto>(connections.Count);

        foreach (var c in connections)
        {
            var pending = await db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.Queued, ct);
            var deadLetter = await db.BackgroundJobs.CountAsync(j => j.Status == BackgroundJobStatus.DeadLettered, ct);
            var failedEvents = await db.SyncEvents.CountAsync(e => e.PsaConnectionId == c.Id && e.Error != null, ct);

            result.Add(new ConnectionHealthDto(
                c.Id, c.Name, c.Provider, c.Status, c.LastSuccessfulSyncAt, c.LastHealthCheckAt,
                pending, deadLetter, failedEvents, c.LastError));
        }
        return result;
    }
}

public sealed class AuditQueryService(DeskDbContext db, ITenantContext tenant) : IAuditQueryService
{
    public async Task<IReadOnlyList<AuditEntryDto>> ListAsync(int take = 100, string? action = null, CancellationToken ct = default)
    {
        // AuditLogEntry is not tenant-filtered by the DbContext, so scope it explicitly here.
        var q = db.AuditLog.AsNoTracking().Where(a => a.MspOrganizationId == tenant.OrganizationId);
        if (!string.IsNullOrEmpty(action)) q = q.Where(a => a.Action == action);
        return await q.OrderByDescending(a => a.CreatedAt).Take(Math.Clamp(take, 1, 500))
            .Select(a => new AuditEntryDto(a.Id, a.Action, a.EntityType, a.EntityId, a.ActorDisplayName, a.CorrelationId, a.CreatedAt, a.DetailJson))
            .ToListAsync(ct);
    }
}

public sealed class UserAdminService(DeskDbContext db, IAuditWriter audit, ITenantContext tenant) : IUserAdminService
{
    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct = default)
    {
        // AppUser is not tenant-filtered by the DbContext, so scope to the caller's org explicitly.
        var users = await db.AppUsers.AsNoTracking()
            .Where(u => u.MspOrganizationId == tenant.OrganizationId)
            .Include(u => u.Roles).ToListAsync(ct);
        var roleIds = users.SelectMany(u => u.Roles.Select(r => r.RoleId)).Distinct().ToList();
        var roleNames = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        return users.Select(u => new UserSummary(
            u.Id, u.Email, u.DisplayName, u.IsActive,
            u.Roles.Select(r => roleNames.GetValueOrDefault(r.RoleId, "?")).ToList())).ToList();
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var exists = await db.UserRoles.AnyAsync(r => r.AppUserId == userId && r.RoleId == roleId, ct);
        if (!exists)
        {
            db.UserRoles.Add(new Domain.Identity.UserRole { AppUserId = userId, RoleId = roleId });
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("user.role.assigned", "AppUser", userId.ToString(), new { roleId }, ct);
        }
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var link = await db.UserRoles.FirstOrDefaultAsync(r => r.AppUserId == userId && r.RoleId == roleId, ct);
        if (link is not null)
        {
            db.UserRoles.Remove(link);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("user.role.removed", "AppUser", userId.ToString(), new { roleId }, ct);
        }
    }
}
