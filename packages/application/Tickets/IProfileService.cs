namespace Desk.Application.Tickets;

/// <summary>
/// The signed-in user's own profile, whoever they are.
///
/// The previous profile endpoint resolved only client-portal identities and threw
/// for everyone else — so a technician, manager or MSP admin opening their own
/// profile page got an error. This service resolves BOTH populations: staff
/// (<c>AppUser</c>) first, then client (<c>ClientUser</c>), because a user who is
/// both (the local dev admin, for instance) is primarily staff.
/// </summary>
public interface IProfileService
{
    Task<ProfileDto?> GetAsync(string idpSubject, CancellationToken ct = default);

    /// <summary>
    /// Self-service update. Deliberately narrow: display name and contact email
    /// only. Role is NOT here — a control that lets a user raise their own role
    /// is a privilege escalation, so roles change only through admin user
    /// management, and the profile page says so instead of pretending.
    /// </summary>
    Task<ProfileDto> UpdateAsync(string idpSubject, string displayName, string email, CancellationToken ct = default);
}

/// <param name="Kind">"staff" or "client" — decides which facts the page can show.</param>
/// <param name="Roles">
/// Staff: role names. Client: "Company administrator" or "Client user". Read-only
/// by design; the UI explains where roles are managed rather than offering an edit.
/// </param>
/// <param name="SignInManaged">
/// True when sign-in is bound to the identity provider (an IdP subject exists), in
/// which case changing the contact email here does not change how the user logs in.
/// </param>
public sealed record ProfileDto(
    string Kind,
    string DisplayName,
    string Email,
    IReadOnlyList<string> Roles,
    DateTimeOffset MemberSince,
    string? CompanyName,
    bool IsCompanyAdministrator,
    bool SignInManaged);
