namespace Desk.Domain.ControlPanel;

/// <summary>
/// Sections of the client control panel. A client company administrator implicitly has every
/// section; other client users only have the sections explicitly granted to them (optionally
/// scoped to specific accounts). Values are stable — persisted on <see cref="ClientAccessGrant"/>.
/// </summary>
public enum ControlPanelSection
{
    /// <summary>Ticket service instructions the technicians follow (CP-1).</summary>
    TicketInstructions = 0,
    /// <summary>Manage the client's own portal users and their access (CP-1, admin only).</summary>
    Users = 1,
    /// <summary>Accounts &amp; devices (CP-2).</summary>
    Accounts = 2,
    /// <summary>Approvers (CP-2).</summary>
    Approvers = 3,
    /// <summary>Escalation procedures (CP-2).</summary>
    Escalation = 4,
    /// <summary>Business hours (CP-2).</summary>
    BusinessHours = 5,
    /// <summary>Holidays (CP-2).</summary>
    Holidays = 6,
    /// <summary>Announcements / bulletin (CP-3).</summary>
    Announcements = 7,
    /// <summary>Reports (CP-3).</summary>
    Reports = 8,
    /// <summary>Portal branding — display name, logo, accent color (CP-3).</summary>
    Branding = 9,
}
