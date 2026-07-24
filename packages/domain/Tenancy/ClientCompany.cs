using Desk.Domain.Common;

namespace Desk.Domain.Tenancy;

/// <summary>
/// A client company under a specific PSA connection. Different clients within one MSP
/// may be routed to different PSA connections (even different providers).
/// </summary>
public class ClientCompany : TenantEntity
{
    public Guid PsaConnectionId { get; set; }
    public PsaConnection? PsaConnection { get; set; }

    public required string Name { get; set; }

    /// <summary>Identifier of the matching company/account in the external PSA.</summary>
    public required string ExternalCompanyId { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ClientUser> Users { get; set; } = new List<ClientUser>();
}
