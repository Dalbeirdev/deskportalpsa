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
}
