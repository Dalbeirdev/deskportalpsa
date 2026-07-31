using Desk.Domain.Common;
using Desk.Domain.Tenancy;

namespace Desk.Domain.ControlPanel;

/// <summary>
/// An announcement the client posts for their own portal users (CP-3). Client-authored, per account.
/// </summary>
public class Announcement : TenantEntity, IAccountScoped
{
    public Guid ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    public required string Title { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Pinned announcements sort to the top.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Draft announcements are hidden from the client's users until published.</summary>
    public bool IsPublished { get; set; } = true;

    public DateTimeOffset? PublishedAt { get; set; }

    public string? AuthorName { get; set; }
}

/// <summary>
/// A knowledge-base / FAQ entry the client maintains for the account (CP-4). Client-authored,
/// grouped by an optional free-text category, and hidden from readers until published.
/// </summary>
public class FaqArticle : TenantEntity, IAccountScoped
{
    public Guid ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    /// <summary>The question / article title.</summary>
    public required string Question { get; set; }

    /// <summary>The answer / article body (plain text or markdown).</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Optional grouping label, e.g. "Email", "Access", "Billing".</summary>
    public string? Category { get; set; }

    /// <summary>Draft articles are hidden from readers until published.</summary>
    public bool IsPublished { get; set; } = true;

    public int SortOrder { get; set; }
}

/// <summary>
/// Portal branding for an account (CP-3): one row per account. Purely presentational metadata the
/// client controls — a display name, a logo URL and an accent color.
/// </summary>
public class ClientBranding : TenantEntity, IAccountScoped
{
    public Guid ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>URL to the client's logo (stored as a link; not an uploaded asset in CP-3).</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Hex accent color, e.g. "#2563eb".</summary>
    public string? AccentColor { get; set; }
}
