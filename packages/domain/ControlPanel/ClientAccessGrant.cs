using Desk.Domain.Common;
using Desk.Domain.Tenancy;

namespace Desk.Domain.ControlPanel;

/// <summary>
/// A per-section (and optionally per-account) access grant for a client portal user.
///
/// The company administrator delegates access "according to their requirement": each grant opens
/// one <see cref="ControlPanelSection"/> for one <see cref="ClientUser"/>, scoped either to a
/// specific account (<see cref="ClientCompanyId"/> set) or to all accounts the user can see
/// (<see cref="ClientCompanyId"/> null). Company administrators need no grants — they implicitly
/// have every section for their company.
/// </summary>
public class ClientAccessGrant : TenantEntity
{
    public Guid ClientUserId { get; set; }
    public ClientUser? ClientUser { get; set; }

    public ControlPanelSection Section { get; set; }

    /// <summary>Null = all accounts the user can see; otherwise scoped to this specific account.</summary>
    public Guid? ClientCompanyId { get; set; }
}
