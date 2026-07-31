using Desk.Application.Common;
using Desk.Application.ControlPanel;
using Desk.Application.Tickets;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.ControlPanel;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tickets;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class ControlPanelTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid AdminUser = Guid.NewGuid();
    private static readonly Guid RegularUser = Guid.NewGuid();

    private static ClientAccess AdminAccess => new(Org, CompanyA, AdminUser, true);
    private static ClientAccess UserAccess => new(Org, CompanyA, RegularUser, false);

    private static (ControlPanelService svc, AdminHarness h) Build()
    {
        var h = AdminHarness.Create(Org);
        h.Db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "CW", Provider = ProviderType.ConnectWisePsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        h.Db.ClientCompanies.Add(new ClientCompany { Id = CompanyA, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "Acme Dental", ExternalCompanyId = "1" });
        h.Db.ClientUsers.Add(new ClientUser { Id = AdminUser, MspOrganizationId = Org, ClientCompanyId = CompanyA, Email = "admin@a.test", DisplayName = "Admin A", IdpSubject = "sub-admin", IsCompanyAdministrator = true });
        h.Db.ClientUsers.Add(new ClientUser { Id = RegularUser, MspOrganizationId = Org, ClientCompanyId = CompanyA, Email = "user@a.test", DisplayName = "User A", IdpSubject = "sub-user" });
        h.Db.SaveChanges();
        var audit = new AuditWriter(h.Db, h.User, h.Tenant, h.Clock);
        return (new ControlPanelService(h.Db, audit), h);
    }

    // ---- Instructions ----

    [Fact]
    public async Task Admin_saves_global_and_account_instructions_and_reads_them_back()
    {
        var (svc, _) = Build();

        await svc.SaveInstructionAsync(AdminAccess, null, "Global: escalate new-user tickets.");
        await svc.SaveInstructionAsync(AdminAccess, CompanyA, "Account: verify O365 first.");

        var view = await svc.GetInstructionsAsync(AdminAccess);
        view.Global.Body.Should().Be("Global: escalate new-user tickets.");
        view.Global.Scope.Should().Be("global");
        view.Accounts.Should().ContainSingle();
        view.Accounts[0].Body.Should().Be("Account: verify O365 first.");
        view.Accounts[0].AccountName.Should().Be("Acme Dental");
    }

    [Fact]
    public async Task Saving_an_instruction_upserts_the_single_row_per_scope()
    {
        var (svc, h) = Build();
        await svc.SaveInstructionAsync(AdminAccess, null, "first");
        await svc.SaveInstructionAsync(AdminAccess, null, "second");

        h.Db.TicketInstructions.Count(i => i.ClientCompanyId == null).Should().Be(1);
        (await svc.GetInstructionsAsync(AdminAccess)).Global.Body.Should().Be("second");
    }

    [Fact]
    public async Task Non_admin_without_grant_cannot_read_or_write_instructions()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.GetInstructionsAsync(UserAccess));
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.SaveInstructionAsync(UserAccess, null, "x"));
    }

    [Fact]
    public async Task Granted_user_can_edit_own_account_but_not_a_foreign_account()
    {
        var (svc, _) = Build();
        // Admin grants the regular user the TicketInstructions section.
        await svc.SetUserAccessAsync(AdminAccess, RegularUser, new SetAccessInput(false,
            new[] { new AccessGrantDto("ticketInstructions", null) }));

        await svc.SaveInstructionAsync(UserAccess, CompanyA, "user edited own account"); // allowed
        (await svc.GetInstructionsAsync(UserAccess)).Accounts[0].Body.Should().Be("user edited own account");

        var foreign = Guid.NewGuid();
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.SaveInstructionAsync(UserAccess, foreign, "nope"));
    }

    [Fact]
    public async Task Ticket_detail_surfaces_account_instructions_over_global()
    {
        var (svc, h) = Build();
        await svc.SaveInstructionAsync(AdminAccess, null, "GLOBAL");
        await svc.SaveInstructionAsync(AdminAccess, CompanyA, "ACCOUNT");

        var ticket = new Ticket
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.ConnectWisePsa,
            ExternalTicketId = "T1", ClientCompanyId = CompanyA, RequesterUserId = AdminUser,
            RequesterName = "R", RequesterEmail = "r@test", Title = "T", PortalStatus = "NEW", PortalPriority = "NORMAL",
        };
        h.Db.Tickets.Add(ticket);
        await h.Db.SaveChangesAsync();

        var reads = new TicketReadService(h.Db);
        var detail = await reads.GetDetailAsync(AdminAccess, ticket.Id);
        detail!.ServiceInstructions.Should().Be("ACCOUNT");
    }

    [Fact]
    public async Task Ticket_detail_falls_back_to_global_when_no_account_override()
    {
        var (svc, h) = Build();
        await svc.SaveInstructionAsync(AdminAccess, null, "GLOBAL-ONLY");

        var ticket = new Ticket
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.ConnectWisePsa,
            ExternalTicketId = "T2", ClientCompanyId = CompanyA, RequesterUserId = AdminUser,
            RequesterName = "R", RequesterEmail = "r@test", Title = "T", PortalStatus = "NEW", PortalPriority = "NORMAL",
        };
        h.Db.Tickets.Add(ticket);
        await h.Db.SaveChangesAsync();

        var detail = await new TicketReadService(h.Db).GetDetailAsync(AdminAccess, ticket.Id);
        detail!.ServiceInstructions.Should().Be("GLOBAL-ONLY");
    }

    // ---- Users & access ----

    [Fact]
    public async Task Admin_invites_user_and_duplicate_email_is_rejected()
    {
        var (svc, _) = Build();
        var created = await svc.InviteUserAsync(AdminAccess, new InviteClientUserInput("new@a.test", "New User", false));
        created.Email.Should().Be("new@a.test");
        created.IsCompanyAdministrator.Should().BeFalse();

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            svc.InviteUserAsync(AdminAccess, new InviteClientUserInput("new@a.test", "Dup", false)));
    }

    [Fact]
    public async Task Non_admin_cannot_manage_users()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.ListUsersAsync(UserAccess));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            svc.InviteUserAsync(UserAccess, new InviteClientUserInput("x@a.test", "X", false)));
    }

    [Fact]
    public async Task Set_user_access_replaces_grants_and_capabilities_reflect_them()
    {
        var (svc, _) = Build();
        await svc.SetUserAccessAsync(AdminAccess, RegularUser, new SetAccessInput(false,
            new[] { new AccessGrantDto("ticketInstructions", CompanyA) }));

        var caps = await svc.GetCapabilitiesAsync(UserAccess);
        caps.IsCompanyAdministrator.Should().BeFalse();
        caps.Sections.Should().ContainSingle().Which.Should().Be("ticketInstructions");

        // Replacing with an empty grant set clears access.
        await svc.SetUserAccessAsync(AdminAccess, RegularUser, new SetAccessInput(false, Array.Empty<AccessGrantDto>()));
        (await svc.GetCapabilitiesAsync(UserAccess)).Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task Admin_capabilities_include_every_section()
    {
        var (svc, _) = Build();
        var caps = await svc.GetCapabilitiesAsync(AdminAccess);
        caps.IsCompanyAdministrator.Should().BeTrue();
        caps.Sections.Should().Contain(new[] { "ticketInstructions", "users", "reports" });
    }

    [Fact]
    public async Task Cannot_disable_or_demote_the_last_administrator()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ValidationFailedException>(() => svc.SetUserActiveAsync(AdminAccess, AdminUser, false));
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            svc.SetUserAccessAsync(AdminAccess, AdminUser, new SetAccessInput(false, Array.Empty<AccessGrantDto>())));
    }

    [Fact]
    public async Task Promoting_a_second_admin_then_demoting_the_first_is_allowed()
    {
        var (svc, _) = Build();
        // Promote the regular user to admin, then the original admin can be demoted.
        await svc.SetUserAccessAsync(AdminAccess, RegularUser, new SetAccessInput(true, Array.Empty<AccessGrantDto>()));
        await svc.SetUserAccessAsync(AdminAccess, AdminUser, new SetAccessInput(false, Array.Empty<AccessGrantDto>()));

        // The former regular user is now an administrator (as the resolver would report on the next request).
        var promotedAccess = new ClientAccess(Org, CompanyA, RegularUser, true);
        var users = await svc.ListUsersAsync(promotedAccess);
        users.Single(u => u.Id == AdminUser).IsCompanyAdministrator.Should().BeFalse();
        users.Single(u => u.Id == RegularUser).IsCompanyAdministrator.Should().BeTrue();
    }
}
