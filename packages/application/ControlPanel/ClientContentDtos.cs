namespace Desk.Application.ControlPanel;

public sealed record AnnouncementDto(
    Guid Id, string Title, string Body, bool IsPinned, bool IsPublished,
    DateTimeOffset? PublishedAt, string? AuthorName);

public sealed record AnnouncementInput(
    Guid? Id, string Title, string? Body, bool IsPinned, bool IsPublished);

public sealed record BrandingDto(string? DisplayName, string? LogoUrl, string? AccentColor);
public sealed record BrandingInput(string? DisplayName, string? LogoUrl, string? AccentColor);

public sealed record FaqArticleDto(Guid Id, string Question, string Answer, string? Category, bool IsPublished, int SortOrder);
public sealed record FaqArticleInput(Guid? Id, string Question, string? Answer, string? Category, bool IsPublished, int SortOrder);

/// <summary>A rendered report ready to download (file name + CSV text).</summary>
public sealed record ReportExportDto(string FileName, string Csv);

public sealed record ReportScheduleDto(
    Guid Id, string Name, string Frequency, string? Recipients, bool IsEnabled,
    DateTimeOffset? LastRunAt, DateTimeOffset NextRunAt);

public sealed record ReportScheduleInput(Guid? Id, string Name, string Frequency, string? Recipients, bool IsEnabled);

public sealed record ReportRunDto(
    Guid Id, Guid? ReportScheduleId, DateTimeOffset GeneratedAt, string Format,
    string Summary, bool Delivered, string? DeliveryNote);

public sealed record StatusCount(string Status, int Count);

public sealed record ReportTicket(Guid Id, string? ExternalTicketId, string Title, string PortalStatus, DateTimeOffset CreatedAt);

/// <summary>Read-only ticket summary for the account (computed from synced tickets).</summary>
public sealed record AccountReportDto(
    int TotalTickets,
    int OpenTickets,
    IReadOnlyList<StatusCount> ByStatus,
    decimal HoursLogged,
    decimal BillableHours,
    IReadOnlyList<ReportTicket> Recent);
