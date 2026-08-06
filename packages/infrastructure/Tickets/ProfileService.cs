using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Application.Tickets;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Tickets;

/// <summary>
/// Profile for every kind of signed-in user.
///
/// Staff resolves first: a user who exists on both sides (the local dev admin is
/// client-linked so the portal pages have data) is primarily staff, and their
/// profile should say "MSP administrator", not "Company administrator".
/// </summary>
public sealed class ProfileService(DeskDbContext db, IAuditWriter audit) : IProfileService
{
    public async Task<ProfileDto?> GetAsync(string idpSubject, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idpSubject)) return null;

        var staff = await db.AppUsers
            .AsNoTracking()
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.IdpSubject == idpSubject && u.IsActive, ct);
        if (staff is not null) return StaffDto(staff);

        var client = await db.ClientUsers
            .AsNoTracking()
            .Include(u => u.ClientCompany)
            .FirstOrDefaultAsync(u => u.IdpSubject == idpSubject && u.IsActive, ct);
        return client is null ? null : ClientDto(client);
    }

    public async Task<ProfileDto> UpdateAsync(
        string idpSubject, string displayName, string email, CancellationToken ct = default)
    {
        displayName = displayName.Trim();
        email = email.Trim();
        if (displayName.Length is < 2 or > 120)
            throw new ValidationFailedException("Display name must be between 2 and 120 characters.");
        if (email.Length > 254 || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
            throw new ValidationFailedException("That does not look like an email address.");

        var staff = await db.AppUsers
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.IdpSubject == idpSubject && u.IsActive, ct);
        if (staff is not null)
        {
            // The audit row carries before -> after: a changed contact email on
            // an admin account is exactly the kind of quiet edit worth a trail.
            var before = new { staff.DisplayName, staff.Email };
            staff.DisplayName = displayName;
            staff.Email = email;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("profile.updated", nameof(Desk.Domain.Identity.AppUser), staff.Id.ToString(),
                new { before, after = new { displayName, email } }, ct);
            return StaffDto(staff);
        }

        var client = await db.ClientUsers
            .Include(u => u.ClientCompany)
            .FirstOrDefaultAsync(u => u.IdpSubject == idpSubject && u.IsActive, ct);
        if (client is null)
            throw new ForbiddenException("No active account is linked to this sign-in.");

        var clientBefore = new { client.DisplayName, client.Email };
        client.DisplayName = displayName;
        client.Email = email;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("profile.updated", nameof(Desk.Domain.Tenancy.ClientUser), client.Id.ToString(),
            new { before = clientBefore, after = new { displayName, email } }, ct);
        return ClientDto(client);
    }

    private static ProfileDto StaffDto(Desk.Domain.Identity.AppUser u) => new(
        Kind: "staff",
        DisplayName: u.DisplayName,
        Email: u.Email,
        Roles: u.Roles
            .Select(r => r.Role?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .OrderBy(n => n)
            .ToArray(),
        MemberSince: u.CreatedAt,
        CompanyName: null,
        IsCompanyAdministrator: false,
        SignInManaged: u.IdpSubject is not null);

    private static ProfileDto ClientDto(Desk.Domain.Tenancy.ClientUser u) => new(
        Kind: "client",
        DisplayName: u.DisplayName,
        Email: u.Email,
        Roles: [u.IsCompanyAdministrator ? "Company administrator" : "Client user"],
        MemberSince: u.CreatedAt,
        CompanyName: u.ClientCompany?.Name,
        IsCompanyAdministrator: u.IsCompanyAdministrator,
        SignInManaged: u.IdpSubject is not null);
}
