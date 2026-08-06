using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Tickets;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The profile every role can actually load — and the edits it deliberately
/// refuses to offer.
///
/// The old endpoint resolved only client identities, so every staff member got a
/// 403 on their own profile page. And the first mockup put an Edit button on
/// Role, which is privilege escalation with good typography: these tests pin
/// that the service exposes no route from a profile edit to a role change.
/// </summary>
public class ProfileTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static (ProfileService svc, AdminHarness h) Build()
    {
        var h = AdminHarness.Create(Org);
        return (new ProfileService(h.Db, new AuditWriter(h.Db, h.User, h.Tenant, h.Clock)), h);
    }

    private static AppUser Staff(string subject, params string[] roleNames)
    {
        var user = new AppUser
        {
            MspOrganizationId = Org,
            Email = "tech@msp.test",
            DisplayName = "Terry Technician",
            IdpSubject = subject,
        };
        foreach (var name in roleNames)
        {
            var role = new Role { MspOrganizationId = Org, Name = name, BuiltInType = RoleType.Technician };
            user.Roles.Add(new UserRole { AppUser = user, Role = role });
        }
        return user;
    }

    [Fact]
    public async Task Staff_users_get_a_profile_instead_of_a_403()
    {
        var (svc, h) = Build();
        h.Db.AppUsers.Add(Staff("tech-sub", "Technician", "Manager"));
        await h.Db.SaveChangesAsync();

        var dto = await svc.GetAsync("tech-sub");

        dto.Should().NotBeNull();
        dto!.Kind.Should().Be("staff");
        dto.DisplayName.Should().Be("Terry Technician");
        dto.Roles.Should().BeEquivalentTo(["Manager", "Technician"]);
        dto.SignInManaged.Should().BeTrue();
        dto.IsCompanyAdministrator.Should().BeFalse("staff are not client company admins");
    }

    [Fact]
    public async Task Client_users_still_resolve_with_their_company_and_admin_flag()
    {
        var (svc, h) = Build();
        var company = new ClientCompany
        {
            MspOrganizationId = Org, PsaConnectionId = Guid.NewGuid(),
            Name = "Acme Dental", ExternalCompanyId = "42",
        };
        h.Db.ClientCompanies.Add(company);
        h.Db.ClientUsers.Add(new ClientUser
        {
            MspOrganizationId = Org, ClientCompanyId = company.Id, ClientCompany = company,
            Email = "owner@acme.test", DisplayName = "Alice Admin",
            IdpSubject = "client-sub", IsCompanyAdministrator = true,
        });
        await h.Db.SaveChangesAsync();

        var dto = await svc.GetAsync("client-sub");

        dto!.Kind.Should().Be("client");
        dto.CompanyName.Should().Be("Acme Dental");
        dto.IsCompanyAdministrator.Should().BeTrue();
        dto.Roles.Should().ContainSingle().Which.Should().Be("Company administrator");
    }

    [Fact]
    public async Task A_user_on_both_sides_is_primarily_staff()
    {
        // The local dev admin is exactly this shape: an AppUser also linked as a
        // client admin so portal pages have data. Their own profile must say
        // what they are, not what they were linked to for demo purposes.
        var (svc, h) = Build();
        h.Db.AppUsers.Add(Staff("both-sub", "MSP administrator"));
        h.Db.ClientUsers.Add(new ClientUser
        {
            MspOrganizationId = Org, ClientCompanyId = Guid.NewGuid(),
            Email = "same@person.test", DisplayName = "Same Person",
            IdpSubject = "both-sub", IsCompanyAdministrator = true,
        });
        await h.Db.SaveChangesAsync();

        (await svc.GetAsync("both-sub"))!.Kind.Should().Be("staff");
    }

    [Fact]
    public async Task Update_changes_name_and_email_and_leaves_an_audit_trail()
    {
        var (svc, h) = Build();
        h.Db.AppUsers.Add(Staff("tech-sub", "Technician"));
        await h.Db.SaveChangesAsync();

        var dto = await svc.UpdateAsync("tech-sub", "Terri Technician", "terri@msp.test");

        dto.DisplayName.Should().Be("Terri Technician");
        dto.Email.Should().Be("terri@msp.test");
        // The quiet edit is on the record: who, what, before and after.
        h.Db.AuditLog.Should().ContainSingle(a => a.Action == "profile.updated");
    }

    [Fact]
    public async Task Update_cannot_touch_roles_because_no_such_input_exists()
    {
        // The design fix, pinned: the update surface is name + email, full stop.
        // A compile-time guarantee beats a runtime check — assert the contract.
        var (svc, h) = Build();
        h.Db.AppUsers.Add(Staff("tech-sub", "Technician"));
        await h.Db.SaveChangesAsync();

        await svc.UpdateAsync("tech-sub", "New Name", "new@msp.test");

        var roles = h.Db.AppUsers.Single().Roles.Select(r => r.Role!.Name);
        roles.Should().BeEquivalentTo(["Technician"], "a profile edit must never move a role");

        var method = typeof(Desk.Application.Tickets.IProfileService).GetMethod("UpdateAsync")!;
        method.GetParameters().Select(p => p.Name)
            .Should().BeEquivalentTo(["idpSubject", "displayName", "email", "ct"],
                "the contract itself must offer no role parameter");
    }

    [Fact]
    public async Task Garbage_input_is_refused_and_nothing_is_written()
    {
        var (svc, h) = Build();
        h.Db.AppUsers.Add(Staff("tech-sub", "Technician"));
        await h.Db.SaveChangesAsync();

        var tooShort = () => svc.UpdateAsync("tech-sub", "X", "ok@msp.test");
        await tooShort.Should().ThrowAsync<ValidationFailedException>();

        var notEmail = () => svc.UpdateAsync("tech-sub", "Fine Name", "not-an-email");
        await notEmail.Should().ThrowAsync<ValidationFailedException>();

        h.Db.AppUsers.Single().DisplayName.Should().Be("Terry Technician");
        h.Db.AuditLog.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_or_inactive_subjects_resolve_to_nothing()
    {
        var (svc, h) = Build();
        var inactive = Staff("gone-sub", "Technician");
        inactive.IsActive = false;
        h.Db.AppUsers.Add(inactive);
        await h.Db.SaveChangesAsync();

        (await svc.GetAsync("nobody")).Should().BeNull();
        (await svc.GetAsync("gone-sub")).Should().BeNull("deactivated accounts have no profile");

        var update = () => svc.UpdateAsync("nobody", "Some Name", "a@b.test");
        await update.Should().ThrowAsync<ForbiddenException>();
    }
}
