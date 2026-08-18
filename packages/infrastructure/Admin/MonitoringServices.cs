using Desk.Application.Abstractions;
using Desk.Application.Admin;
using Desk.Application.Attachments;
using Desk.Application.Authorization;
using Desk.Application.Common;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Organization;
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
    public async Task<IReadOnlyList<AuditEntryDto>> ListAsync(
        int take = 100, string? action = null, string? entityId = null, CancellationToken ct = default)
    {
        // AuditLogEntry is not tenant-filtered by the DbContext, so scope it explicitly here.
        var q = db.AuditLog.AsNoTracking().Where(a => a.MspOrganizationId == tenant.OrganizationId);
        if (!string.IsNullOrEmpty(action)) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(entityId)) q = q.Where(a => a.EntityId == entityId);
        return await q.OrderByDescending(a => a.CreatedAt).Take(Math.Clamp(take, 1, 500))
            .Select(a => new AuditEntryDto(a.Id, a.Action, a.EntityType, a.EntityId, a.ActorDisplayName, a.CorrelationId, a.CreatedAt, a.DetailJson))
            .ToListAsync(ct);
    }
}

public sealed class UserAdminService(
    DeskDbContext db, IAuditWriter audit, ITenantContext tenant, ICurrentUser currentUser,
    IObjectStorage storage, IEffectivePermissionService permissions, TimeProvider clock) : IUserAdminService
{
    // Only roles this page may hand out. Client roles belong to client-user management; the
    // platform role is cross-tenant and must never be assignable from inside a tenant.
    private static readonly RoleType[] StaffRoleTypes =
        [RoleType.MspAdministrator, RoleType.Manager, RoleType.Technician, RoleType.Auditor];

    /// <summary>Same allowlist and rationale as ConnectionAdminService's logo upload: raster only,
    /// no SVG (it can carry script, and a photo never needs the detail vectors offer).</summary>
    private static readonly Dictionary<string, string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png", ["image/jpeg"] = ".jpg", ["image/webp"] = ".webp", ["image/gif"] = ".gif",
    };
    private const int MaxPhotoBytes = 1024 * 1024;

    public async Task<UserListResultDto> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        // AppUser is not tenant-filtered by the DbContext, so scope to the caller's org explicitly.
        var baseQuery = db.AppUsers.AsNoTracking().Where(u => u.MspOrganizationId == tenant.OrganizationId);

        var filtered = baseQuery;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim().ToLower();
            filtered = filtered.Where(u => u.DisplayName.ToLower().Contains(needle) || u.Email.ToLower().Contains(needle));
        }
        if (query.IsActive is { } isActive) filtered = filtered.Where(u => u.IsActive == isActive);
        if (query.RoleId is { } roleId) filtered = filtered.Where(u => u.Roles.Any(r => r.RoleId == roleId));
        if (query.DepartmentId is { } deptId)
            filtered = filtered.Where(u => db.UserDepartments.Any(ud => ud.AppUserId == u.Id && ud.DepartmentId == deptId));
        if (query.TeamId is { } teamId)
            filtered = filtered.Where(u => db.UserTeams.Any(ut => ut.AppUserId == u.Id && ut.TeamId == teamId));
        if (!string.IsNullOrWhiteSpace(query.BoardName))
            filtered = filtered.Where(u => db.UserBoardGrants.Any(g => g.AppUserId == u.Id && g.BoardName == query.BoardName));

        var totalMatching = await filtered.CountAsync(ct);

        // Summary cards describe the whole organization, not the current filter result, so they're
        // computed from the unfiltered base query — otherwise narrowing a filter to zero rows would
        // make "Total Users" read zero too.
        var total = await baseQuery.CountAsync(ct);
        var active = await baseQuery.CountAsync(u => u.IsActive, ct);
        var pending = await baseQuery.CountAsync(u => u.IdpSubject == null, ct);
        var adminRoleIds = await db.Roles.AsNoTracking()
            .Where(r => r.BuiltInType == RoleType.MspAdministrator).Select(r => r.Id).ToListAsync(ct);
        var admins = await baseQuery.CountAsync(u => u.Roles.Any(r => adminRoleIds.Contains(r.RoleId)), ct);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pageUsers = await filtered
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(u => u.Roles)
            .ToListAsync(ct);

        return new UserListResultDto(
            await ToSummariesAsync(pageUsers, ct), totalMatching, page, pageSize,
            new UserSummaryCountsDto(total, active, pending, admins));
    }

    public async Task<UserSummary?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.AppUsers.AsNoTracking()
            .Where(u => u.Id == userId && u.MspOrganizationId == tenant.OrganizationId)
            .Include(u => u.Roles).SingleOrDefaultAsync(ct);
        if (user is null) return null;
        return (await ToSummariesAsync([user], ct))[0];
    }

    /// <summary>Batch-loads every related table ONCE for the given users rather than per-row, so a
    /// page of 25 users costs a fixed handful of queries, not 25x that.</summary>
    private async Task<List<UserSummary>> ToSummariesAsync(List<Desk.Domain.Identity.AppUser> users, CancellationToken ct)
    {
        var userIds = users.Select(u => u.Id).ToList();

        var roleIds = users.SelectMany(u => u.Roles.Select(r => r.RoleId)).Distinct().ToList();
        var roleNames = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var managerIds = users.Where(u => u.ManagerId.HasValue).Select(u => u.ManagerId!.Value).Distinct().ToList();
        var managerNames = await db.AppUsers.AsNoTracking().Where(u => managerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var userDepartments = await db.UserDepartments.AsNoTracking()
            .Where(ud => userIds.Contains(ud.AppUserId)).Include(ud => ud.Department).ToListAsync(ct);
        var userTeams = await db.UserTeams.AsNoTracking()
            .Where(ut => userIds.Contains(ut.AppUserId)).Include(ut => ut.Team).ToListAsync(ct);
        var boardAccessByUser = await db.UserBoardAccesses.AsNoTracking()
            .Where(a => userIds.Contains(a.AppUserId)).ToDictionaryAsync(a => a.AppUserId, ct);
        var boardGrants = await db.UserBoardGrants.AsNoTracking()
            .Where(g => userIds.Contains(g.AppUserId)).ToListAsync(ct);
        var connectionNames = await db.PsaConnections.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return users.Select(u =>
        {
            var depts = userDepartments.Where(ud => ud.AppUserId == u.Id).ToList();
            var primary = depts.FirstOrDefault(d => d.IsPrimary) ?? depts.FirstOrDefault();
            var secondary = depts.Where(d => d != primary)
                .Select(d => new DepartmentOptionDto(d.DepartmentId, d.Department?.Name ?? "?")).ToList();
            var teams = userTeams.Where(ut => ut.AppUserId == u.Id)
                .Select(ut => new TeamOptionDto(ut.TeamId, ut.Team?.Name ?? "?", ut.Team?.DepartmentId ?? Guid.Empty)).ToList();
            var grants = boardGrants.Where(g => g.AppUserId == u.Id)
                .Select(g => new BoardOptionDto(g.PsaConnectionId, connectionNames.GetValueOrDefault(g.PsaConnectionId, "?"), g.BoardName))
                .ToList();

            return new UserSummary(
                u.Id, u.Email, u.DisplayName, u.IsActive, u.IdpSubject != null,
                u.Roles.Select(r => new RoleOptionDto(r.RoleId, roleNames.GetValueOrDefault(r.RoleId, "?"))).ToList(),
                u.PhoneNumber, u.Location, u.PhotoUrl, u.ManagerId,
                u.ManagerId.HasValue ? managerNames.GetValueOrDefault(u.ManagerId.Value) : null,
                primary is null ? null : new DepartmentOptionDto(primary.DepartmentId, primary.Department?.Name ?? "?"),
                secondary, teams,
                boardAccessByUser.GetValueOrDefault(u.Id)?.Mode ?? BoardAccessMode.All,
                grants, u.LastActiveAt, u.CreatedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<RoleOptionDto>> StaffRolesAsync(CancellationToken ct = default)
        => await db.Roles.AsNoTracking()
            .Where(r => r.BuiltInType != null && StaffRoleTypes.Contains(r.BuiltInType.Value))
            .OrderBy(r => r.BuiltInType)
            .Select(r => new RoleOptionDto(r.Id, r.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DepartmentWithTeamsDto>> DepartmentsAsync(CancellationToken ct = default)
        => await db.Departments.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .Select(d => new DepartmentWithTeamsDto(
                d.Id, d.Name,
                d.Teams.Where(t => t.IsActive).OrderBy(t => t.SortOrder)
                    .Select(t => new TeamOptionDto(t.Id, t.Name, t.DepartmentId)).ToList()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BoardOptionDto>> BoardsAsync(CancellationToken ct = default)
        // Boards are not a stored entity — see the Phase-1 design note on UserBoardGrant — so this
        // is a live derivation from whatever has actually synced, exactly like the Tickets page's
        // own board filter, just computed server-side instead of from already-loaded rows.
        //
        // Ordering by a property read off a positional record (BoardOptionDto) doesn't translate —
        // EF can't see through the constructor to know ConnectionName maps to the join's c.Name.
        // Order on the anonymous projection instead, and only build the DTO in the final Select.
        => await db.Tickets.AsNoTracking()
            .Where(t => t.QueueOrBoard != null)
            .Select(t => new { t.PsaConnectionId, t.QueueOrBoard })
            .Distinct()
            .Join(db.PsaConnections.AsNoTracking(), t => t.PsaConnectionId, c => c.Id,
                (t, c) => new { t.PsaConnectionId, ConnectionName = c.Name, BoardName = t.QueueOrBoard! })
            .OrderBy(b => b.ConnectionName).ThenBy(b => b.BoardName)
            .Select(b => new BoardOptionDto(b.PsaConnectionId, b.ConnectionName, b.BoardName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PermissionTemplateOptionDto>> PermissionTemplatesAsync(CancellationToken ct = default)
        => await db.PermissionTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new PermissionTemplateOptionDto(t.Id, t.Name, t.Description, t.BaseRoleType))
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
        return (await GetAsync(user.Id, ct))!;
    }

    public async Task<UserSummary> UpdateAsync(Guid userId, UpdateStaffUserInput input, CancellationToken ct = default)
    {
        var user = await db.AppUsers.FirstOrDefaultAsync(
            u => u.Id == userId && u.MspOrganizationId == tenant.OrganizationId, ct)
            ?? throw new NotFoundException("User");

        var displayName = input.DisplayName.Trim();
        var email = input.Email.Trim();
        if (displayName.Length is < 2 or > 120)
            throw new ValidationFailedException("Display name must be between 2 and 120 characters.");
        if (email.Length > 254 || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
            throw new ValidationFailedException("That does not look like an email address.");

        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailTaken = await db.AppUsers.AnyAsync(u =>
                u.Id != userId && u.MspOrganizationId == tenant.OrganizationId && u.Email.ToLower() == email.ToLower(), ct);
            if (emailTaken)
                throw new ValidationFailedException("A user with that email already exists.");
        }

        if (input.ManagerId == userId)
            throw new ValidationFailedException("A user cannot be their own manager.");
        if (input.ManagerId is { } managerId)
        {
            var managerExists = await db.AppUsers.AnyAsync(
                u => u.Id == managerId && u.MspOrganizationId == tenant.OrganizationId, ct);
            if (!managerExists)
                throw new ValidationFailedException("That manager could not be found.");
        }

        var before = new { user.DisplayName, user.Email, user.PhoneNumber, user.Location, user.ManagerId };
        user.DisplayName = displayName;
        user.Email = email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        user.Location = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim();
        user.ManagerId = input.ManagerId;
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("user.updated", "AppUser", userId.ToString(),
            new { before, after = new { user.DisplayName, user.Email, user.PhoneNumber, user.Location, user.ManagerId } }, ct);
        return (await GetAsync(userId, ct))!;
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

    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        EnsureNotActingOnSelf(userId);
        var user = await db.AppUsers.FirstOrDefaultAsync(
            u => u.Id == userId && u.MspOrganizationId == tenant.OrganizationId, ct)
            ?? throw new NotFoundException("User");

        var email = user.Email;
        var previousPhotoKey = user.PhotoStorageKey;

        // Every row that references this user, removed before the user itself — none of these
        // tables cascade at the database level (AppUser isn't tenant-scoped in a way that would let
        // a blanket FK cascade be configured safely), so it is done explicitly here.
        db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.AppUserId == userId));
        db.UserDepartments.RemoveRange(db.UserDepartments.Where(d => d.AppUserId == userId));
        db.UserTeams.RemoveRange(db.UserTeams.Where(t => t.AppUserId == userId));
        db.UserBoardAccesses.RemoveRange(db.UserBoardAccesses.Where(a => a.AppUserId == userId));
        db.UserBoardGrants.RemoveRange(db.UserBoardGrants.Where(g => g.AppUserId == userId));
        db.UserPermissionOverrides.RemoveRange(db.UserPermissionOverrides.Where(o => o.AppUserId == userId));
        // Anyone who reported to this user keeps their own row, just loses the (now-dangling)
        // manager reference — Restrict at the FK level means this must happen before the delete.
        await db.AppUsers.Where(u => u.ManagerId == userId).ForEachAsync(u => u.ManagerId = null, ct);
        db.AppUsers.Remove(user);
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousPhotoKey))
            try { await storage.DeleteAsync(previousPhotoKey, ct); } catch { /* best-effort, matches the logo pattern */ }

        await audit.WriteAsync("user.deleted", "AppUser", userId.ToString(), new { email }, ct);
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        EnsureNotActingOnSelf(userId);
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
        EnsureNotActingOnSelf(userId);
        var link = await db.UserRoles.FirstOrDefaultAsync(r => r.AppUserId == userId && r.RoleId == roleId, ct);
        if (link is not null)
        {
            db.UserRoles.Remove(link);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("user.role.removed", "AppUser", userId.ToString(), new { roleId }, ct);
        }
    }

    public async Task SetDepartmentAsync(Guid userId, Guid departmentId, bool isPrimary, CancellationToken ct = default)
    {
        var existing = await db.UserDepartments.FirstOrDefaultAsync(
            d => d.AppUserId == userId && d.DepartmentId == departmentId, ct);

        if (isPrimary)
        {
            // Exactly one primary per user — the partial unique index enforces this at the database
            // level, but un-setting the previous primary here means a caller never hits that
            // constraint as an error; they just see the primary move.
            var currentPrimary = await db.UserDepartments.FirstOrDefaultAsync(
                d => d.AppUserId == userId && d.IsPrimary && d.DepartmentId != departmentId, ct);
            if (currentPrimary is not null) currentPrimary.IsPrimary = false;
        }

        if (existing is not null) existing.IsPrimary = isPrimary;
        else db.UserDepartments.Add(new UserDepartment { AppUserId = userId, DepartmentId = departmentId, IsPrimary = isPrimary });

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("user.department.changed", "AppUser", userId.ToString(), new { departmentId, isPrimary }, ct);
    }

    public async Task RemoveDepartmentAsync(Guid userId, Guid departmentId, CancellationToken ct = default)
    {
        var link = await db.UserDepartments.FirstOrDefaultAsync(d => d.AppUserId == userId && d.DepartmentId == departmentId, ct);
        if (link is not null)
        {
            db.UserDepartments.Remove(link);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("user.department.changed", "AppUser", userId.ToString(), new { departmentId, removed = true }, ct);
        }
    }

    public async Task AssignTeamAsync(Guid userId, Guid teamId, CancellationToken ct = default)
    {
        var exists = await db.UserTeams.AnyAsync(t => t.AppUserId == userId && t.TeamId == teamId, ct);
        if (!exists)
        {
            db.UserTeams.Add(new UserTeam { AppUserId = userId, TeamId = teamId });
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("user.team.changed", "AppUser", userId.ToString(), new { teamId }, ct);
        }
    }

    public async Task RemoveTeamAsync(Guid userId, Guid teamId, CancellationToken ct = default)
    {
        var link = await db.UserTeams.FirstOrDefaultAsync(t => t.AppUserId == userId && t.TeamId == teamId, ct);
        if (link is not null)
        {
            db.UserTeams.Remove(link);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("user.team.changed", "AppUser", userId.ToString(), new { teamId, removed = true }, ct);
        }
    }

    public async Task SetBoardAccessModeAsync(Guid userId, BoardAccessMode mode, CancellationToken ct = default)
    {
        var access = await db.UserBoardAccesses.FirstOrDefaultAsync(a => a.AppUserId == userId, ct);
        if (access is null) db.UserBoardAccesses.Add(new UserBoardAccess { AppUserId = userId, Mode = mode });
        else access.Mode = mode;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("user.board_access.changed", "AppUser", userId.ToString(), new { mode }, ct);
    }

    public async Task SetBoardGrantAsync(Guid userId, Guid psaConnectionId, string boardName, BoardAction actions, CancellationToken ct = default)
    {
        var grant = await db.UserBoardGrants.FirstOrDefaultAsync(
            g => g.AppUserId == userId && g.PsaConnectionId == psaConnectionId && g.BoardName == boardName, ct);
        if (grant is null)
            db.UserBoardGrants.Add(new UserBoardGrant { AppUserId = userId, PsaConnectionId = psaConnectionId, BoardName = boardName, Actions = actions });
        else
            grant.Actions = actions;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("user.board_access.changed", "AppUser", userId.ToString(), new { psaConnectionId, boardName, actions }, ct);
    }

    public async Task RemoveBoardGrantAsync(Guid userId, Guid psaConnectionId, string boardName, CancellationToken ct = default)
    {
        var grant = await db.UserBoardGrants.FirstOrDefaultAsync(
            g => g.AppUserId == userId && g.PsaConnectionId == psaConnectionId && g.BoardName == boardName, ct);
        if (grant is not null)
        {
            db.UserBoardGrants.Remove(grant);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("user.board_access.changed", "AppUser", userId.ToString(),
                new { psaConnectionId, boardName, removed = true }, ct);
        }
    }

    public async Task ApplyPermissionTemplateAsync(Guid userId, Guid templateId, CancellationToken ct = default)
    {
        var template = await db.PermissionTemplates.AsNoTracking()
            .Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new NotFoundException("Permission template");

        foreach (var entry in template.Entries)
        {
            var existing = await db.UserPermissionOverrides.FirstOrDefaultAsync(
                o => o.AppUserId == userId && o.PermissionKey == entry.PermissionKey, ct);
            if (existing is not null)
            {
                existing.Effect = entry.Effect;
                existing.Scope = entry.Scope;
                existing.AppliedFromTemplateId = template.Id;
            }
            else
            {
                db.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    AppUserId = userId, PermissionKey = entry.PermissionKey,
                    Effect = entry.Effect, Scope = entry.Scope, AppliedFromTemplateId = template.Id,
                });
            }
        }
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("user.permission_template.applied", "AppUser", userId.ToString(),
            new { templateId, templateName = template.Name, entryCount = template.Entries.Count }, ct);
    }

    public async Task<IReadOnlyList<EffectivePermissionDto>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var results = new List<EffectivePermissionDto>();
        foreach (var def in PermissionCatalog.Definitions)
        {
            var eff = await permissions.ResolveAsync(userId, def.Key, ct);
            results.Add(new EffectivePermissionDto(
                def.Key, def.Module, def.DisplayName, eff.Scope, eff.Source.ToString(),
                def.IsBoardAware, eff.BoardMode.ToString()));
        }
        return results;
    }

    public async Task<UserSummary> UploadPhotoAsync(Guid userId, UserPhotoUpload upload, CancellationToken ct = default)
    {
        var user = await db.AppUsers.FirstOrDefaultAsync(
            u => u.Id == userId && u.MspOrganizationId == tenant.OrganizationId, ct)
            ?? throw new NotFoundException("User");

        if (!AllowedPhotoTypes.TryGetValue(upload.ContentType ?? "", out var extension))
            throw new ValidationFailedException("Use a PNG, JPEG, WebP or GIF image.");
        if (upload.Content.Length == 0)
            throw new ValidationFailedException("The file is empty.");
        if (upload.Content.Length > MaxPhotoBytes)
            throw new ValidationFailedException("Photos must be 1 MB or smaller.");

        var key = $"user-photos/{userId}-{clock.GetUtcNow().ToUnixTimeMilliseconds()}{extension}";
        await storage.PutAsync(key, upload.Content, upload.ContentType!, ct);

        var previous = user.PhotoStorageKey;
        user.PhotoStorageKey = key;
        user.PhotoUrl = $"/api/bff/api/admin/users/{userId}/photo?v={clock.GetUtcNow().ToUnixTimeMilliseconds()}";
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previous))
            try { await storage.DeleteAsync(previous, ct); } catch { /* best-effort */ }

        await audit.WriteAsync("user.photo.updated", "AppUser", userId.ToString(),
            new { upload.FileName, upload.ContentType, Bytes = upload.Content.Length }, ct);
        return (await GetAsync(userId, ct))!;
    }

    public async Task RemovePhotoAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.AppUsers.FirstOrDefaultAsync(
            u => u.Id == userId && u.MspOrganizationId == tenant.OrganizationId, ct)
            ?? throw new NotFoundException("User");

        var key = user.PhotoStorageKey;
        user.PhotoStorageKey = null;
        user.PhotoUrl = null;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(key))
            try { await storage.DeleteAsync(key, ct); } catch { /* best-effort */ }

        await audit.WriteAsync("user.photo.removed", "AppUser", userId.ToString(), new { }, ct);
    }

    public async Task<StoredLogo?> GetPhotoAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.MspOrganizationId == tenant.OrganizationId, ct);
        if (user?.PhotoStorageKey is not { Length: > 0 } key) return null;

        var bytes = await storage.GetAsync(key, ct);
        if (bytes is null) return null;

        // Served content type is derived from the stored key's extension against the allowlist,
        // never trusted from anywhere else — same rationale as the logo route.
        var extension = Path.GetExtension(key);
        var contentType = AllowedPhotoTypes.FirstOrDefault(p => p.Value == extension).Key ?? "image/png";
        return new StoredLogo(bytes, contentType);
    }

    public async Task<BulkUserActionResultDto> BulkAsync(BulkUserActionInput input, CancellationToken ct = default)
    {
        var rows = new List<BulkUserRowResultDto>();
        foreach (var userId in input.UserIds.Distinct())
        {
            try
            {
                switch (input.Action)
                {
                    case BulkUserAction.AssignRole:
                        await AssignRoleAsync(userId, input.RoleId ?? throw new ValidationFailedException("A role is required."), ct);
                        break;
                    case BulkUserAction.RemoveRole:
                        await RemoveRoleAsync(userId, input.RoleId ?? throw new ValidationFailedException("A role is required."), ct);
                        break;
                    case BulkUserAction.AssignDepartment:
                        await SetDepartmentAsync(userId, input.DepartmentId ?? throw new ValidationFailedException("A department is required."), isPrimary: false, ct);
                        break;
                    case BulkUserAction.AssignTeam:
                        await AssignTeamAsync(userId, input.TeamId ?? throw new ValidationFailedException("A team is required."), ct);
                        break;
                    case BulkUserAction.Activate:
                        await SetActiveAsync(userId, true, ct);
                        break;
                    case BulkUserAction.Deactivate:
                        await SetActiveAsync(userId, false, ct);
                        break;
                    case BulkUserAction.Delete:
                        await DeleteAsync(userId, ct);
                        break;
                }
                rows.Add(new BulkUserRowResultDto(userId, true, null));
            }
            catch (Exception ex) when (ex is DeskException)
            {
                // One row's self-guard or validation failure must not abort the rest of the batch —
                // that is the entire reason this returns per-row outcomes instead of one exception.
                rows.Add(new BulkUserRowResultDto(userId, false, ex.Message));
            }
        }
        return new BulkUserActionResultDto(rows);
    }

    /// <summary>
    /// No one may change their own roles or delete their own account, even while holding
    /// UsersManage/RolesManage. Claim presence alone cannot express this — a holder of RolesManage
    /// genuinely has the permission — so it has to be a same-row-as-actor check in the service
    /// itself. Blanket rather than "only blocks adding a higher role": a narrower rule invites
    /// exactly the edge-case reasoning ("this role isn't ADMIN, so self-assigning it is fine") that
    /// produces the next escalation path. Any legitimate change to your own access is made by
    /// someone else.
    /// </summary>
    private void EnsureNotActingOnSelf(Guid targetUserId)
    {
        if (currentUser.UserId is { } actorId && actorId == targetUserId)
            throw new ForbiddenException("You cannot do this to your own account. Ask another administrator.");
    }
}
