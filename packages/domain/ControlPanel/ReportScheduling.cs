using Desk.Domain.Common;
using Desk.Domain.Tenancy;

namespace Desk.Domain.ControlPanel;

/// <summary>How often a scheduled report is generated.</summary>
public enum ReportFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
}

/// <summary>
/// A recurring report the client schedules for their account (part of the Reports section). The
/// worker generates a <see cref="ReportRun"/> each time the schedule falls due and hands it to the
/// delivery pipeline; the run is always available for download in the portal regardless of email.
/// </summary>
public class ReportSchedule : TenantEntity, IAccountScoped
{
    public Guid ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    public required string Name { get; set; }
    public ReportFrequency Frequency { get; set; } = ReportFrequency.Weekly;

    /// <summary>Comma-separated email recipients for delivery (used once an email provider is configured).</summary>
    public string? Recipients { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>When the schedule next falls due. The worker picks up schedules whose NextRunAt is in the past.</summary>
    public DateTimeOffset NextRunAt { get; set; }
}

/// <summary>
/// A generated report instance — produced on demand ("Run now" / manual export) or by a schedule.
/// The rendered content (CSV) is stored so the client can download historical runs from the portal.
/// </summary>
public class ReportRun : TenantEntity, IAccountScoped
{
    public Guid ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    /// <summary>Null for a manual/on-demand run; otherwise the schedule that produced it.</summary>
    public Guid? ReportScheduleId { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Export format label, e.g. "csv".</summary>
    public string Format { get; set; } = "csv";

    /// <summary>Short human summary, e.g. "12 tickets · 34.50h".</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The rendered report content (CSV text).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True once handed to the delivery pipeline (email). Portal download works regardless.</summary>
    public bool Delivered { get; set; }

    public string? DeliveryNote { get; set; }
}
