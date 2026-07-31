using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Application.ControlPanel;
using Desk.Application.Tickets;
using Desk.Domain.ControlPanel;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.ControlPanel;

/// <summary>
/// CP-3 content service. Announcements + branding are per-account and gated by section access;
/// the report is a read-only projection of the account's synced tickets. Timestamps come from the
/// shared clock (via SaveChanges) so results stay deterministic.
/// </summary>
public sealed class ClientContentService(DeskDbContext db, IAuditWriter audit, TimeProvider clock) : IClientContentService
{
    private static readonly string[] OpenStatuses = ["NEW", "IN_PROGRESS", "WAITING_CUSTOMER", "ON_HOLD"];

    // ---- Announcements ----

    public async Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Announcements, ct);
        return await db.Announcements.AsNoTracking()
            .Where(a => a.ClientCompanyId == access.ClientCompanyId)
            .OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.IsPinned, a.IsPublished, a.PublishedAt, a.AuthorName))
            .ToListAsync(ct);
    }

    public async Task<AnnouncementDto> SaveAnnouncementAsync(ClientAccess access, AnnouncementInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Announcements, ct);
        var title = (input.Title ?? "").Trim();
        if (title.Length == 0) throw new ValidationFailedException("Title is required.");

        var author = await db.ClientUsers.AsNoTracking()
            .Where(u => u.Id == access.ClientUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);

        Announcement row;
        if (input.Id is { } id)
        {
            row = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && a.ClientCompanyId == access.ClientCompanyId, ct)
                  ?? throw new NotFoundException("Announcement");
        }
        else
        {
            row = new Announcement { ClientCompanyId = access.ClientCompanyId, Title = title };
            db.Announcements.Add(row);
        }

        row.Title = title;
        row.Body = input.Body ?? "";
        row.IsPinned = input.IsPinned;
        // Stamp the publish time when it first becomes published.
        if (input.IsPublished && (!row.IsPublished || row.PublishedAt is null)) row.PublishedAt = clock.GetUtcNow();
        row.IsPublished = input.IsPublished;
        row.AuthorName = author;
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("control_panel.announcement.save", nameof(Announcement), row.Id.ToString(), new { row.IsPublished }, ct);
        return new AnnouncementDto(row.Id, row.Title, row.Body, row.IsPinned, row.IsPublished, row.PublishedAt, row.AuthorName);
    }

    public async Task DeleteAnnouncementAsync(ClientAccess access, Guid id, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Announcements, ct);
        var row = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && a.ClientCompanyId == access.ClientCompanyId, ct)
                  ?? throw new NotFoundException("Announcement");
        db.Announcements.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.announcement.delete", nameof(Announcement), id.ToString(), null, ct);
    }

    // ---- Branding ----

    public async Task<BrandingDto> GetBrandingAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Branding, ct);
        var row = await db.ClientBrandings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientCompanyId == access.ClientCompanyId, ct);
        return new BrandingDto(row?.DisplayName, row?.LogoUrl, row?.AccentColor);
    }

    public async Task<BrandingDto> SaveBrandingAsync(ClientAccess access, BrandingInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Branding, ct);
        var row = await db.ClientBrandings.FirstOrDefaultAsync(x => x.ClientCompanyId == access.ClientCompanyId, ct);
        if (row is null)
        {
            row = new ClientBranding { ClientCompanyId = access.ClientCompanyId };
            db.ClientBrandings.Add(row);
        }
        row.DisplayName = Trim(input.DisplayName);
        row.LogoUrl = Trim(input.LogoUrl);
        row.AccentColor = Trim(input.AccentColor);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.branding.save", nameof(ClientBranding), row.Id.ToString(), null, ct);
        return new BrandingDto(row.DisplayName, row.LogoUrl, row.AccentColor);
    }

    // ---- Report ----

    public async Task<AccountReportDto> GetReportAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.Reports, ct);

        var tickets = db.Tickets.AsNoTracking().Where(t => t.ClientCompanyId == access.ClientCompanyId);

        var byStatus = await tickets
            .GroupBy(t => t.PortalStatus)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync(ct);

        var total = byStatus.Sum(s => s.Count);
        var open = byStatus.Where(s => OpenStatuses.Contains(s.Status.ToUpper())).Sum(s => s.Count);
        var hours = await tickets.SumAsync(t => (decimal?)t.TimeWorkedHours, ct) ?? 0m;
        var billable = await tickets.SumAsync(t => (decimal?)t.BillableHours, ct) ?? 0m;

        var recent = await tickets
            .OrderByDescending(t => t.CreatedAt)
            .Take(8)
            .Select(t => new ReportTicket(t.Id, t.ExternalTicketId, t.Title, t.PortalStatus, t.CreatedAt))
            .ToListAsync(ct);

        return new AccountReportDto(total, open,
            byStatus.OrderByDescending(s => s.Count).ToList(),
            Math.Round(hours, 2), Math.Round(billable, 2), recent);
    }

    // ---- Knowledge base / FAQ ----

    public async Task<IReadOnlyList<FaqArticleDto>> ListFaqAsync(ClientAccess access, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.KnowledgeBase, ct);
        return await db.FaqArticles.AsNoTracking()
            .Where(f => f.ClientCompanyId == access.ClientCompanyId)
            .OrderBy(f => f.Category).ThenBy(f => f.SortOrder).ThenBy(f => f.Question)
            .Select(f => new FaqArticleDto(f.Id, f.Question, f.Answer, f.Category, f.IsPublished, f.SortOrder))
            .ToListAsync(ct);
    }

    public async Task<FaqArticleDto> SaveFaqAsync(ClientAccess access, FaqArticleInput input, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.KnowledgeBase, ct);
        var question = (input.Question ?? "").Trim();
        if (question.Length == 0) throw new ValidationFailedException("Question is required.");

        FaqArticle row;
        if (input.Id is { } id)
        {
            row = await db.FaqArticles.FirstOrDefaultAsync(f => f.Id == id && f.ClientCompanyId == access.ClientCompanyId, ct)
                  ?? throw new NotFoundException("FAQ article");
        }
        else
        {
            row = new FaqArticle { ClientCompanyId = access.ClientCompanyId, Question = question };
            db.FaqArticles.Add(row);
        }

        row.Question = question;
        row.Answer = input.Answer ?? "";
        row.Category = Trim(input.Category);
        row.IsPublished = input.IsPublished;
        row.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("control_panel.faq.save", nameof(FaqArticle), row.Id.ToString(), new { row.IsPublished }, ct);
        return new FaqArticleDto(row.Id, row.Question, row.Answer, row.Category, row.IsPublished, row.SortOrder);
    }

    public async Task DeleteFaqAsync(ClientAccess access, Guid id, CancellationToken ct = default)
    {
        await EnsureSectionAsync(access, ControlPanelSection.KnowledgeBase, ct);
        var row = await db.FaqArticles.FirstOrDefaultAsync(f => f.Id == id && f.ClientCompanyId == access.ClientCompanyId, ct)
                  ?? throw new NotFoundException("FAQ article");
        db.FaqArticles.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("control_panel.faq.delete", nameof(FaqArticle), id.ToString(), null, ct);
    }

    // ---- Helpers ----

    private async Task EnsureSectionAsync(ClientAccess access, ControlPanelSection section, CancellationToken ct)
    {
        if (access.IsCompanyAdministrator) return;
        var granted = await db.ClientAccessGrants.AsNoTracking()
            .AnyAsync(g => g.ClientUserId == access.ClientUserId && g.Section == section, ct);
        if (!granted) throw new ForbiddenException("You do not have access to this section.");
    }

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
