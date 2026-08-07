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

public sealed class UserAdminService(DeskDbContext db, IAuditWriter audit, ITenantContext tenant, ICurrentUser currentUser) : IUserAdminService
{
    // Only roles this page may hand out. Client roles belong to client-user management; the
    // platform role is cross-tenant and must never be assignable from inside a tenant.
    private static readonly RoleType[] StaffRoleTypes =
        [RoleType.MspAdministrator, RoleType.Manager, RoleType.Technician, RoleType.Auditor];

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
            u.Id, u.Email, u.DisplayName, u.IsActive, u.IdpSubject != null,
            u.Roles.Select(r => new RoleOptionDto(r.RoleId, roleNames.GetValueOrDefault(r.RoleId, "?"))).ToList())).ToList();
    }

    public async Task<IReadOnlyList<RoleOptionDto>> StaffRolesAsync(CancellationToken ct = default)
        => await db.Roles.AsNoTracking()
            .Where(r => r.BuiltInType != null && StaffRoleTypes.Contains(r.BuiltInType.Value))
            .OrderBy(r => r.BuiltInType)
            .Select(r => new RoleOptionDto(r.Id, r.Name))
            .ToListAsync(ct);

    public async Task<UserSummary> CreateAsync(CreateStaffUserInput input, CancellationToken ct = default)
    {
        var displayName = input.DisplayName.Trim();
        var email = input.Email.Trim();
        if (displayName.Length is < 2 or > 120)
            throw new ValidationFailedException("Display name must be between 2 and 120 characters.");
        if (email.Length > 254 || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
            throw new ValidationFailedException("That does not look like an email address.");
        if (input.RoleIds.Count == 0)
            throw new ValidationFailedException("Pick at least one role — a user with none can do nothing.");

        // The email is the sign-in binding key, so a duplicate would make the binding ambiguous.
        var emailTaken = await db.AppUsers.AnyAsync(u =>
            u.MspOrganizationId == tenant.OrganizationId && u.Email.ToLower() == email.ToLower(), ct);
        if (emailTaken)
            throw new ValidationFailedException("A user with that email already exists.");

        var validRoles = await db.Roles
            .Where(r => input.RoleIds.Contains(r.Id)
                        && r.BuiltInType != null && StaffRoleTypes.Contains(r.BuiltInType.Value))
            .Select(r => new RoleOptionDto(r.Id, r.Name))
            .ToListAsync(ct);
        if (validRoles.Count != input.RoleIds.Distinct().Count())
            throw new ValidationFailedException("One of those roles cannot be assigned to staff.");

        var user = new Desk.Domain.Identity.AppUser
        {
            MspOrganizationId = tenant.OrganizationId,
            DisplayName = displayName,
            Email = email,
            // Deliberately null: sign-in is IdP-managed, and the subject arrives the first time
            // this person logs in with a token whose verified email matches. Until then the row is
            // an invitation, and the UI says so.
            IdpSubject = null,
            IsActive = true,
        };
        foreach (var role in validRoles)
            user.Roles.Add(new Desk.Domain.Identity.UserRole { RoleId = role.Id });
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("user.created", "AppUser", user.Id.ToString(),
            new { displayName, email, roles = validRoles.Select(r => r.Name) }, ct);
        return new UserSummary(user.Id, email, displayName, true, false, validRoles);
    }

    public async Task SetActiveAsync(Guid userId, bool active, CancellationToken ct = default)
    {
        var user = await db.AppUsers.FirstOrDefaultAsync(
            u => u.Id == userId && u.MspOrganizationId == tenant.OrganizationId, ct)
            ?? throw new NotFoundException("User");

        // Locking yourself out is never what was meant, and in the worst case removes the last
        // person able to undo it.
        if (!active && user.IdpSubject == currentUser.Subject)
            throw new ValidationFailedException("You cannot deactivate your own account.");

        user.IsActive = active;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(active ? "user.activated" : "user.deactivated", "AppUser", user.Id.ToString(),
            new { user.Email }, ct);
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
