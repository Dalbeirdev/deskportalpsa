using Desk.Application.Tickets;

namespace Desk.Application.ControlPanel;

/// <summary>
/// CP-3 content: announcements the client posts for their own users, portal branding, and a
/// read-only ticket report for the account. Scoped to the caller's account and gated per section.
/// </summary>
public interface IClientContentService
{
    Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(ClientAccess access, CancellationToken ct = default);
    Task<AnnouncementDto> SaveAnnouncementAsync(ClientAccess access, AnnouncementInput input, CancellationToken ct = default);
    Task DeleteAnnouncementAsync(ClientAccess access, Guid id, CancellationToken ct = default);

    Task<BrandingDto> GetBrandingAsync(ClientAccess access, CancellationToken ct = default);
    Task<BrandingDto> SaveBrandingAsync(ClientAccess access, BrandingInput input, CancellationToken ct = default);

    Task<AccountReportDto> GetReportAsync(ClientAccess access, CancellationToken ct = default);

    Task<IReadOnlyList<FaqArticleDto>> ListFaqAsync(ClientAccess access, CancellationToken ct = default);
    Task<FaqArticleDto> SaveFaqAsync(ClientAccess access, FaqArticleInput input, CancellationToken ct = default);
    Task DeleteFaqAsync(ClientAccess access, Guid id, CancellationToken ct = default);

    // Reports — export + scheduling (Reports section).
    Task<ReportExportDto> ExportReportAsync(ClientAccess access, CancellationToken ct = default);

    Task<IReadOnlyList<ReportScheduleDto>> ListSchedulesAsync(ClientAccess access, CancellationToken ct = default);
    Task<ReportScheduleDto> SaveScheduleAsync(ClientAccess access, ReportScheduleInput input, CancellationToken ct = default);
    Task DeleteScheduleAsync(ClientAccess access, Guid id, CancellationToken ct = default);
    /// <summary>Generate a report for the schedule right now (also runs the delivery pipeline).</summary>
    Task<ReportRunDto> RunScheduleNowAsync(ClientAccess access, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ReportRunDto>> ListRunsAsync(ClientAccess access, CancellationToken ct = default);
    Task<ReportExportDto> DownloadRunAsync(ClientAccess access, Guid runId, CancellationToken ct = default);
}
