using Desk.Domain.Common;
using Desk.Domain.Tenancy;

namespace Desk.Domain.ControlPanel;

/// <summary>
/// Ticket service instructions that technicians follow when working a client's tickets.
///
/// Two levels, mirroring how MSPs actually run this:
///   • <see cref="ClientCompanyId"/> is null → the organization-wide default ("for every account").
///   • <see cref="ClientCompanyId"/> is set  → an override for that specific client company/account.
///
/// Exactly one row exists per scope (enforced by a unique index on
/// (MspOrganizationId, ClientCompanyId)); saving upserts that single row.
/// </summary>
public class TicketInstruction : TenantEntity
{
    /// <summary>Null = organization-wide default; otherwise the specific account these apply to.</summary>
    public Guid? ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    /// <summary>Free-text instructions shown to technicians on the ticket.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Display name of the client user who last edited these instructions (for the audit trail shown in-app).</summary>
    public string? LastEditedBy { get; set; }
}
