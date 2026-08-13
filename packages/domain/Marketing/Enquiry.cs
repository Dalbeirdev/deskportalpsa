using Desk.Domain.Common;

namespace Desk.Domain.Marketing;

public enum EnquiryKind
{
    /// <summary>General "contact us" message.</summary>
    Contact = 0,

    /// <summary>A request for a meeting, carrying the times the person suggested.</summary>
    Meeting = 1,
}

public enum EnquiryStatus
{
    New = 0,
    InProgress = 1,
    Closed = 2,
}

/// <summary>
/// Someone reaching in from the public site. Deliberately NOT <see cref="ITenantScoped"/>: an
/// enquiry arrives before any tenant relationship exists, so stamping one would either invent a
/// tenant or hide the row behind a filter no anonymous submitter can satisfy.
///
/// Enquiries are stored rather than emailed because this deployment has no mail transport — a form
/// that pretends to send and silently drops the lead is worse than no form at all.
/// </summary>
public sealed class Enquiry : BaseEntity
{
    public EnquiryKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Phone { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>When the person asked to meet, in their words — free text beats a picker that
    /// silently converts time zones and books the wrong hour.</summary>
    public string? PreferredTime { get; set; }

    public EnquiryStatus Status { get; set; } = EnquiryStatus.New;

    /// <summary>Which page the form was on, so it is clear what they had just read.</summary>
    public string? SourcePage { get; set; }
}
