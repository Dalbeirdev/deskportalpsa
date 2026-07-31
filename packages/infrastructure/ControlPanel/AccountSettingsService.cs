using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Application.ControlPanel;
using Desk.Application.Tickets;
using Desk.Domain.Common;
using Desk.Domain.ControlPanel;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.ControlPanel;

/// <summary>
/// Per-account settings CRUD. Every query is tenant-isolated by the DbContext filter and further
/// constrained to the caller's account; each section enforces the caller's control-panel access
/// (administrator, or a grant for that section) and audits mutations.
/// </summary>
public sealed class AccountSettingsService(DeskDbContext db, IAuditWriter audit) : IAccountSettingsService
{
    // ---- Account (read-only projection of the client company) ----

    public async Task<AccountDto> GetAccountAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Accounts, ct);
        var c = await db.ClientCompanies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == access.ClientCompanyId, ct)
            ?? throw new NotFoundException("Account");
        var conn = await db.PsaConnections.AsNoTracking()
            .Where(p => p.Id == c.PsaConnectionId).Select(p => p.Name).FirstOrDefaultAsync(ct);
        return new AccountDto(c.Id, c.Name, c.ExternalCompanyId, conn, c.IsActive);
    }

    // ---- Approvers ----

    public async Task<IReadOnlyList<ApproverDto>> ListApproversAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Approvers, ct);
        return await Scoped(db.Approvers, access).OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .Select(a => new ApproverDto(a.Id, a.Name, a.Email, a.Phone, a.Scope, a.SortOrder)).ToListAsync(ct);
    }

    public async Task<ApproverDto> SaveApproverAsync(ClientAccess access, ApproverInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Approvers, ct);
        var name = Required(input.Name, "Name");
        var row = await FindOrNew(db.Approvers, access, input.Id, () => new Approver { ClientCompanyId = access.ClientCompanyId, Name = name }, ct);
        row.Name = name; row.Email = Trim(input.Email); row.Phone = Trim(input.Phone); row.Scope = Trim(input.Scope); row.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.approver.save", nameof(Approver), row.Id.ToString(), null, ct);
        return new ApproverDto(row.Id, row.Name, row.Email, row.Phone, row.Scope, row.SortOrder);
    }

    public Task DeleteApproverAsync(ClientAccess access, Guid id, CancellationToken ct = default)
        => DeleteAsync(db.Approvers, access, id, ControlPanelSection.Approvers, "control_panel.approver.delete", nameof(Approver), ct);

    // ---- Escalation ----

    public async Task<IReadOnlyList<EscalationLevelDto>> ListEscalationAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Escalation, ct);
        return await Scoped(db.EscalationLevels, access).OrderBy(e => e.Level)
            .Select(e => new EscalationLevelDto(e.Id, e.Level, e.Name, e.Contact, e.Condition)).ToListAsync(ct);
    }

    public async Task<EscalationLevelDto> SaveEscalationAsync(ClientAccess access, EscalationLevelInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Escalation, ct);
        var name = Required(input.Name, "Name");
        var row = await FindOrNew(db.EscalationLevels, access, input.Id, () => new EscalationLevel { ClientCompanyId = access.ClientCompanyId, Name = name }, ct);
        row.Name = name; row.Level = input.Level < 1 ? 1 : input.Level; row.Contact = Trim(input.Contact); row.Condition = Trim(input.Condition);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.escalation.save", nameof(EscalationLevel), row.Id.ToString(), null, ct);
        return new EscalationLevelDto(row.Id, row.Level, row.Name, row.Contact, row.Condition);
    }

    public Task DeleteEscalationAsync(ClientAccess access, Guid id, CancellationToken ct = default)
        => DeleteAsync(db.EscalationLevels, access, id, ControlPanelSection.Escalation, "control_panel.escalation.delete", nameof(EscalationLevel), ct);

    // ---- Holidays ----

    public async Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Holidays, ct);
        return await Scoped(db.Holidays, access).OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name)).ToListAsync(ct);
    }

    public async Task<HolidayDto> SaveHolidayAsync(ClientAccess access, HolidayInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Holidays, ct);
        var date = Required(input.Date, "Date");
        var name = Required(input.Name, "Name");
        var row = await FindOrNew(db.Holidays, access, input.Id, () => new Holiday { ClientCompanyId = access.ClientCompanyId, Date = date, Name = name }, ct);
        row.Date = date; row.Name = name;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.holiday.save", nameof(Holiday), row.Id.ToString(), null, ct);
        return new HolidayDto(row.Id, row.Date, row.Name);
    }

    public Task DeleteHolidayAsync(ClientAccess access, Guid id, CancellationToken ct = default)
        => DeleteAsync(db.Holidays, access, id, ControlPanelSection.Holidays, "control_panel.holiday.delete", nameof(Holiday), ct);

    // ---- Devices ----

    public async Task<IReadOnlyList<DeviceDto>> ListDevicesAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Accounts, ct);
        return await Scoped(db.Devices, access).OrderBy(d => d.Name)
            .Select(d => new DeviceDto(d.Id, d.Name, d.Type, d.Identifier, d.Notes)).ToListAsync(ct);
    }

    public async Task<DeviceDto> SaveDeviceAsync(ClientAccess access, DeviceInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Accounts, ct);
        var name = Required(input.Name, "Name");
        var row = await FindOrNew(db.Devices, access, input.Id, () => new Device { ClientCompanyId = access.ClientCompanyId, Name = name }, ct);
        row.Name = name; row.Type = Trim(input.Type); row.Identifier = Trim(input.Identifier); row.Notes = Trim(input.Notes);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.device.save", nameof(Device), row.Id.ToString(), null, ct);
        return new DeviceDto(row.Id, row.Name, row.Type, row.Identifier, row.Notes);
    }

    public Task DeleteDeviceAsync(ClientAccess access, Guid id, CancellationToken ct = default)
        => DeleteAsync(db.Devices, access, id, ControlPanelSection.Accounts, "control_panel.device.delete", nameof(Device), ct);

    // ---- Business hours (single row per account) ----

    public async Task<BusinessHoursDto> GetBusinessHoursAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.BusinessHours, ct);
        var row = await Scoped(db.BusinessHours, access).AsNoTracking().FirstOrDefaultAsync(ct);
        return new BusinessHoursDto(row?.TimeZone, row?.ScheduleJson ?? "[]", row?.Notes);
    }

    public async Task<BusinessHoursDto> SaveBusinessHoursAsync(ClientAccess access, BusinessHoursInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.BusinessHours, ct);
        var row = await Scoped(db.BusinessHours, access).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new BusinessHours { ClientCompanyId = access.ClientCompanyId };
            db.BusinessHours.Add(row);
        }
        row.TimeZone = Trim(input.TimeZone);
        row.ScheduleJson = string.IsNullOrWhiteSpace(input.ScheduleJson) ? "[]" : input.ScheduleJson;
        row.Notes = Trim(input.Notes);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.business_hours.save", nameof(BusinessHours), row.Id.ToString(), null, ct);
        return new BusinessHoursDto(row.TimeZone, row.ScheduleJson, row.Notes);
    }

    // ---- Helpers ----

    private static IQueryable<T> Scoped<T>(DbSet<T> set, ClientAccess access) where T : BaseEntity, IAccountScoped
        => set.Where(x => x.ClientCompanyId == access.ClientCompanyId);

    private static async Task<T> FindOrNew<T>(DbSet<T> set, ClientAccess access, Guid? id, Func<T> factory, CancellationToken ct)
        where T : BaseEntity, IAccountScoped
    {
        if (id is null)
        {
            var created = factory();
            set.Add(created);
            return created;
        }
        return await Scoped(set, access).FirstOrDefaultAsync(x => x.Id == id, ct)
               ?? throw new NotFoundException(typeof(T).Name);
    }

    private async Task DeleteAsync<T>(DbSet<T> set, ClientAccess access, Guid id, ControlPanelSection section,
        string action, string entityType, CancellationToken ct) where T : BaseEntity, IAccountScoped
    {
        await EnsureSectionAsync(access, section, ct);
        var row = await Scoped(set, access).FirstOrDefaultAsync(x => x.Id == id, ct)
                  ?? throw new NotFoundException(entityType);
        set.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(action, entityType, id.ToString(), null, ct);
    }

    private async Task EnsureSectionAsync(ClientAccess access, ControlPanelSection section, CancellationToken ct)
    {
        if (access.IsCompanyAdministrator) return;
        var granted = await db.ClientAccessGrants.AsNoTracking()
            .AnyAsync(g => g.ClientUserId == access.ClientUserId && g.Section == section, ct);
        if (!granted) throw new ForbiddenException($"You do not have access to this section.");
    }

    private static string Required(string? v, string field)
    {
        var t = (v ?? "").Trim();
        if (t.Length == 0) throw new ValidationFailedException($"{field} is required.");
        return t;
    }

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
