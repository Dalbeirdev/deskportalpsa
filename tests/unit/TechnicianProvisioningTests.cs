using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Connectors.Mock;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Authorization;
using Desk.PsaCore.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Bringing PSA technicians into the portal. Per-technician analytics with one shared staff account
/// measures nothing, so this is the step that makes the rest of the productivity layer mean
/// anything — but it creates real logins on a real tenant, so what it REFUSES to create matters as
/// much as what it does.
/// </summary>
public class TechnicianProvisioningTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    private static async Task<(AdminHarness H, TechnicianProvisioningService Svc, MockConnector Psa)> BuildAsync()
    {
        var h = AdminHarness.Create(Org);
        h.Db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "Autotask", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x", IsEnabled = true,
        });
        // The built-in Technician role a provisioned user is given.
        h.Db.Roles.Add(new Role { Name = "Technician", IsSystemRole = true, BuiltInType = RoleType.Technician });
        await h.Db.SaveChangesAsync();

        var psa = new MockConnector(new MockConnectorOptions(), h.Clock);
        var users = new UserAdminService(h.Db, new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), h.Tenant, h.User,
            new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock), new EffectivePermissionService(h.Db), h.Clock);
        var svc = new TechnicianProvisioningService(h.Db, new FakeResolver(psa), users,
            new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), h.Tenant);
        return (h, svc, psa);
    }

    [Fact]
    public async Task Provisioning_creates_the_portal_user_and_maps_them_to_the_psa()
    {
        var (h, svc, psa) = await BuildAsync();
        psa.AddTechnician("29682889", "basit@techpio.test", "Basit Lone");

        var summary = await svc.ProvisionAsync(Conn, "29682889");

        summary.Email.Should().Be("basit@techpio.test");
        summary.Roles.Should().ContainSingle(r => r.Name == "Technician");
        var identity = await h.Db.UserPsaIdentities.SingleAsync();
        identity.AppUserId.Should().Be(summary.Id);
        identity.ExternalTechnicianId.Should().Be("29682889",
            "without the mapping their logged time still falls to the connection default");
    }

    [Fact]
    public async Task An_existing_portal_user_is_linked_rather_than_duplicated()
    {
        // Matched on email, which is also the sign-in binding key — a second row for the same
        // person would make that binding ambiguous.
        var (h, svc, psa) = await BuildAsync();
        h.Db.AppUsers.Add(new AppUser
        { MspOrganizationId = Org, Email = "basit@techpio.test", DisplayName = "Basit Lone" });
        await h.Db.SaveChangesAsync();
        psa.AddTechnician("29682889", "basit@techpio.test", "Basit Lone");

        await svc.ProvisionAsync(Conn, "29682889");

        (await h.Db.AppUsers.CountAsync()).Should().Be(1, "the person already had an account");
        (await h.Db.UserPsaIdentities.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_technician_with_no_email_is_refused_and_told_why()
    {
        // Almost always an API user or service account. The portal binds sign-in by verified email,
        // so an account without one could never be logged into — creating it would be pure clutter
        // on a real tenant, and clutter that looks like a person.
        var (h, svc, psa) = await BuildAsync();
        psa.AddTechnician("999", "", "Autotask API User");

        var act = async () => await svc.ProvisionAsync(Conn, "999");

        (await act.Should().ThrowAsync<ValidationFailedException>())
            .Which.Message.Should().Contain("no email address");
        (await h.Db.AppUsers.AnyAsync()).Should().BeFalse("nothing may be created for an unusable account");
    }

    [Fact]
    public async Task Provisioning_twice_is_idempotent()
    {
        var (h, svc, psa) = await BuildAsync();
        psa.AddTechnician("29682889", "basit@techpio.test", "Basit Lone");

        await svc.ProvisionAsync(Conn, "29682889");
        await svc.ProvisionAsync(Conn, "29682889");

        (await h.Db.AppUsers.CountAsync()).Should().Be(1);
        (await h.Db.UserPsaIdentities.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_list_separates_linked_from_matched_from_new()
    {
        // The screen's whole job is showing what still needs doing, so the three states must be
        // distinguishable — and the ones with nothing to do sort last.
        var (h, svc, psa) = await BuildAsync();
        psa.AddTechnician("1", "linked@techpio.test", "Already Linked");
        psa.AddTechnician("2", "known@techpio.test", "Known By Email");
        psa.AddTechnician("3", "new@techpio.test", "Brand New");
        psa.AddTechnician("4", "", "API User");
        h.Db.AppUsers.Add(new AppUser
        { MspOrganizationId = Org, Email = "known@techpio.test", DisplayName = "Known By Email" });
        await h.Db.SaveChangesAsync();
        await svc.ProvisionAsync(Conn, "1");

        var rows = await svc.ListAsync(Conn);

        rows.Single(r => r.ExternalId == "1").Link.Should().Be(PsaTechnicianLink.Linked);
        rows.Single(r => r.ExternalId == "2").Link.Should().Be(PsaTechnicianLink.MatchedByEmail);
        rows.Single(r => r.ExternalId == "3").Link.Should().Be(PsaTechnicianLink.NotInPortal);
        var apiUser = rows.Single(r => r.ExternalId == "4");
        apiUser.CanProvision.Should().BeFalse();
        apiUser.Blocker.Should().Contain("No email");
        rows.Last().Link.Should().Be(PsaTechnicianLink.Linked, "what is done sorts to the bottom");
    }
}
