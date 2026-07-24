using Desk.Domain.Common;

namespace Desk.Domain.Tenancy;

/// <summary>
/// Top-level tenant. Every business record in the platform is scoped to one MSP organization.
/// A single MSP may hold many PSA connections across many providers.
/// </summary>
public class MspOrganization : BaseEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
    public string? BrandingLogoUrl { get; set; }
    public string TimeZone { get; set; } = "UTC";

    public ICollection<PsaConnection> PsaConnections { get; set; } = new List<PsaConnection>();
    public ICollection<ClientCompany> ClientCompanies { get; set; } = new List<ClientCompany>();
}
