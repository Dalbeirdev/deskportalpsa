namespace Desk.Application.ControlPanel;

public sealed record AccountDto(
    Guid Id,
    string Name,
    string ExternalCompanyId,
    string? ConnectionName,
    bool IsActive);

public sealed record ApproverDto(Guid Id, string Name, string? Email, string? Phone, string? Scope, int SortOrder);
public sealed record ApproverInput(Guid? Id, string Name, string? Email, string? Phone, string? Scope, int SortOrder);

public sealed record EscalationLevelDto(Guid Id, int Level, string Name, string? Contact, string? Condition);
public sealed record EscalationLevelInput(Guid? Id, int Level, string Name, string? Contact, string? Condition);

public sealed record HolidayDto(Guid Id, string Date, string Name);
public sealed record HolidayInput(Guid? Id, string Date, string Name);

public sealed record DeviceDto(Guid Id, string Name, string? Type, string? Identifier, string? Notes);
public sealed record DeviceInput(Guid? Id, string Name, string? Type, string? Identifier, string? Notes);

public sealed record BusinessHoursDto(string? TimeZone, string ScheduleJson, string? Notes);
public sealed record BusinessHoursInput(string? TimeZone, string ScheduleJson, string? Notes);
