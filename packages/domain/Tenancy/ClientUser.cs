using Desk.Domain.Common;

namespace Desk.Domain.Tenancy;

/// <summary>An end user belonging to a client company who can raise and view tickets.</summary>
public class ClientUser : TenantEntity
{
    public Guid ClientCompanyId { get; set; }
    public ClientCompany? ClientCompany { get; set; }

    public required string Email { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>Subject (sub) claim from the identity provider, if this user has signed in.</summary>
    public string? IdpSubject { get; set; }

    /// <summary>Identifier of the matching contact in the external PSA.</summary>
    public string? ExternalContactId { get; set; }

    public bool IsCompanyAdministrator { get; set; }
    public bool IsActive { get; set; } = true;
}
