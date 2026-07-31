using Desk.Application.Common;
using Desk.Application.ControlPanel;
using Desk.Application.Tickets;
using Desk.Domain.ControlPanel;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.ControlPanel;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class AccountSettingsTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid AdminUser = Guid.NewGuid();
    private static readonly Guid RegularUser = Guid.NewGuid();

    private static ClientAccess AdminAccess => new(Org, CompanyA, AdminUser, true);
    private static ClientAccess UserAccess => new(Org, CompanyA, RegularUser, false);

    private static (AccountSettingsService svc, AdminHarness h) Build()
    {
        var h = AdminHarness.Create(Org);
        h.Db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "CW", Provider = ProviderType.ConnectWisePsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        h.Db.ClientCompanies.Add(new ClientCompany { Id = CompanyA, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "Acme Dental", ExternalCompanyId = "42" });
        h.Db.ClientUsers.Add(new ClientUser { Id = AdminUser, MspOrganizationId = Org, ClientCompanyId = CompanyA, Email = "admin@a.test", DisplayName = "Admin A", IdpSubject = "sub-admin", IsCompanyAdministrator = true });
        h.Db.ClientUsers.Add(new ClientUser { Id = RegularUser, MspOrganizationId = Org, ClientCompanyId = CompanyA, Email = "user@a.test", DisplayName = "User A", IdpSubject = "sub-user" });
        h.Db.SaveChanges();
        return (new AccountSettingsService(h.Db, new AuditWriter(h.Db, h.User, h.Tenant, h.Clock)), h);
    }

    [Fact]
    public async Task Account_read_projects_the_client_company_with_connection_name()
    {
        var (svc, _) = Build();
        var acc = await svc.GetAccountAsync(AdminAccess);
        acc.Name.Should().Be("Acme Dental");
        acc.ExternalCompanyId.Should().Be("42");
        acc.ConnectionName.Should().Be("CW");
        acc.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Approver_create_update_delete_roundtrip()
    {
        var (svc, _) = Build();
        var created = await svc.SaveApproverAsync(AdminAccess, new ApproverInput(null, "Jane", "j@a.test", "555", "New users", 1));
        created.Id.Should().NotBeEmpty();
        (await svc.ListApproversAsync(AdminAccess)).Should().ContainSingle();

        var updated = await svc.SaveApproverAsync(AdminAccess, new ApproverInput(created.Id, "Jane Doe", "j@a.test", "555", "New users + purchases", 1));
        updated.Name.Should().Be("Jane Doe");
        (await svc.ListApproversAsync(AdminAccess)).Should().ContainSingle(); // updated in place, not duplicated

        await svc.DeleteApproverAsync(AdminAccess, created.Id);
        (await svc.ListApproversAsync(AdminAccess)).Should().BeEmpty();
    }

    [Fact]
    public async Task Approver_requires_a_name()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            svc.SaveApproverAsync(AdminAccess, new ApproverInput(null, "  ", null, null, null, 0)));
    }

    [Fact]
    public async Task Escalation_levels_list_in_order()
    {
        var (svc, _) = Build();
        await svc.SaveEscalationAsync(AdminAccess, new EscalationLevelInput(null, 2, "Tier 2", "eng@a.test", "P1"));
        await svc.SaveEscalationAsync(AdminAccess, new EscalationLevelInput(null, 1, "Tier 1", "hd@a.test", "No response 30m"));
        var list = await svc.ListEscalationAsync(AdminAccess);
        list.Select(x => x.Level).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task Business_hours_upserts_a_single_row()
    {
        var (svc, h) = Build();
        await svc.SaveBusinessHoursAsync(AdminAccess, new BusinessHoursInput("America/New_York", "[{\"day\":\"Mon\",\"open\":true}]", "note1"));
        await svc.SaveBusinessHoursAsync(AdminAccess, new BusinessHoursInput("America/Chicago", "[]", "note2"));

        h.Db.BusinessHours.Count(b => b.ClientCompanyId == CompanyA).Should().Be(1);
        var bh = await svc.GetBusinessHoursAsync(AdminAccess);
        bh.TimeZone.Should().Be("America/Chicago");
        bh.Notes.Should().Be("note2");
    }

    [Fact]
    public async Task Holiday_and_device_crud_work()
    {
        var (svc, _) = Build();
        var hol = await svc.SaveHolidayAsync(AdminAccess, new HolidayInput(null, "2026-07-04", "Independence Day"));
        (await svc.ListHolidaysAsync(AdminAccess)).Should().ContainSingle();
        await svc.DeleteHolidayAsync(AdminAccess, hol.Id);
        (await svc.ListHolidaysAsync(AdminAccess)).Should().BeEmpty();

        var dev = await svc.SaveDeviceAsync(AdminAccess, new DeviceInput(null, "Reception PC", "Workstation", "ASSET-1", "Front desk"));
        (await svc.ListDevicesAsync(AdminAccess)).Should().ContainSingle();
        await svc.DeleteDeviceAsync(AdminAccess, dev.Id);
        (await svc.ListDevicesAsync(AdminAccess)).Should().BeEmpty();
    }

    [Fact]
    public async Task Non_admin_without_grant_is_forbidden_and_a_grant_opens_the_section()
    {
        var (svc, h) = Build();
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.ListApproversAsync(UserAccess));

        // Grant the regular user the Approvers section.
        h.Db.ClientAccessGrants.Add(new ClientAccessGrant { ClientUserId = RegularUser, Section = ControlPanelSection.Approvers, MspOrganizationId = Org });
        await h.Db.SaveChangesAsync();

        await svc.SaveApproverAsync(UserAccess, new ApproverInput(null, "Granted", null, null, null, 0)); // now allowed
        (await svc.ListApproversAsync(UserAccess)).Should().ContainSingle();

        // But a section they were NOT granted stays closed.
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.ListHolidaysAsync(UserAccess));
    }

    [Fact]
    public async Task Deleting_a_foreign_id_is_not_found()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<NotFoundException>(() => svc.DeleteApproverAsync(AdminAccess, Guid.NewGuid()));
    }
}
