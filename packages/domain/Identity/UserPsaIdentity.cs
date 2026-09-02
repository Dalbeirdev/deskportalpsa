using Desk.Domain.Common;
using Desk.Domain.Tenancy;

namespace Desk.Domain.Identity;

/// <summary>
/// Who this staff user IS inside one PSA — the resource/member that work logged from the portal
/// should be attributed to.
///
/// Per CONNECTION, not one field on <see cref="AppUser"/>, because the same person has a different
/// identifier in every PSA and the identifiers are not even the same KIND of thing: Autotask wants
/// a numeric resourceID, ConnectWise wants a member identifier string. An MSP running both would
/// have one value that is necessarily wrong for one of them — and silently wrong, since a bad
/// identifier is only discovered when a technician's logged hour is rejected.
///
/// Absent is a valid state, and the common one: with no row, time falls back to the connection's
/// default time-entry resource exactly as before.
/// </summary>
public class UserPsaIdentity : BaseEntity, ITenantScoped
{
    public Guid MspOrganizationId { get; set; }

    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }

    public Guid PsaConnectionId { get; set; }
    public PsaConnection? PsaConnection { get; set; }

    /// <summary>The provider's own identifier for this person. Opaque here — each connector knows
    /// what shape its own provider expects.</summary>
    public required string ExternalTechnicianId { get; set; }

    /// <summary>Display name as the PSA gave it, so the admin screen can show who this maps to
    /// without a round-trip to the provider.</summary>
    public string? ExternalTechnicianName { get; set; }
}
