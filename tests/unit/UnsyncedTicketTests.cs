using Desk.Application.Mapping;
using Desk.Domain.Enums;
using Desk.Domain.Mapping;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Admin;
using Desk.Infrastructure.Sync;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Tickets that never reached the PSA: being able to count them, and push them again. A rejected
/// create used to throw before anything was written, so the customer's ticket was simply gone —
/// no count, no id, nothing to retry from.
/// </summary>
public class UnsyncedTicketTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    private static async Task<AdminHarness> SeedAsync(string? defaultQueue = null)
    {
        var h = AdminHarness.Create(Org);
        h.Db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "Autotask", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x", DefaultQueueOrBoardId = defaultQueue,
        });
        h.Db.ClientCompanies.Add(new ClientCompany
        {
            Id = Company, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "Acme", ExternalCompanyId = "176",
        });
        await h.Db.SaveChangesAsync();
        return h;
    }

    private static Ticket Unsynced(string title, string? error = "queueID is required") => new()
    {
        MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.AutotaskPsa,
        ExternalTicketId = null, ClientCompanyId = Company,
        RequesterName = "R", RequesterEmail = "r@acme.test", Title = title,
        PortalStatus = "NEW", PortalPriority = "HIGH",
        SyncStatus = TicketSyncStatus.Error, SyncError = error, CorrelationId = Guid.NewGuid(),
    };

    private static TicketResyncService Service(AdminHarness h, IServiceManagementConnector c) =>
        new(h.Db, new FakeResolver(c), new MappingEngine(),
            new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), h.Clock);

    [Fact]
    public async Task Unsynced_tickets_are_counted_and_identified()
    {
        var h = await SeedAsync();
        await using var _ = h.Db;
        h.Db.Tickets.Add(Unsynced("Printer down"));
        h.Db.Tickets.Add(Unsynced("VPN broken"));
        var synced = Unsynced("Already there");
        synced.ExternalTicketId = "7810";
        synced.SyncStatus = TicketSyncStatus.Synced;
        synced.SyncError = null;
        h.Db.Tickets.Add(synced);
        await h.Db.SaveChangesAsync();

        var result = await Service(h, new StubConnector()).ListAsync();

        result.Count.Should().Be(2);
        result.Tickets.Select(t => t.Title).Should().BeEquivalentTo("Printer down", "VPN broken");
        // The id is what a human needs to find the ticket again, so it must be on every row.
        result.Tickets.Should().OnlyContain(t => t.TicketId != Guid.Empty);
        result.Tickets.Should().OnlyContain(t => t.ConnectionName == "Autotask");
        result.Tickets.Should().OnlyContain(t => t.CustomerName == "Acme");
        result.Tickets.Should().OnlyContain(t => t.SyncError != null);
    }

    [Fact]
    public async Task A_ticket_with_no_external_id_counts_as_unsynced_even_if_its_status_says_otherwise()
    {
        var h = await SeedAsync();
        await using var _ = h.Db;
        var t = Unsynced("Mislabelled", error: null);
        t.SyncStatus = TicketSyncStatus.Synced; // status claims success, but nothing is in the PSA
        h.Db.Tickets.Add(t);
        await h.Db.SaveChangesAsync();

        // The absence of a provider id is the fact; the status column is only a claim about it.
        (await Service(h, new StubConnector()).ListAsync()).Count.Should().Be(1);
    }

    [Fact]
    public async Task Resync_uses_the_connections_current_board_default_rather_than_what_failed_before()
    {
        // The commonest rejection is a missing queue. Configure the default, press resync, and it
        // must go through without the customer re-entering anything.
        var h = await SeedAsync(defaultQueue: "29682833");
        await using var _ = h.Db;
        var ticket = Unsynced("Printer down");
        h.Db.Tickets.Add(ticket);
        await h.Db.SaveChangesAsync();

        var stub = new StubConnector { NextCreateResult = new CreateTicketResult(true, "7811", null) };
        var result = await Service(h, stub).ResyncAsync(ticket.Id);

        result.Success.Should().BeTrue();
        result.ExternalTicketId.Should().Be("7811");
        stub.CreateRequests.Should().ContainSingle().Which.QueueOrBoard.Should().Be("29682833");

        var row = await h.Db.Tickets.SingleAsync();
        row.ExternalTicketId.Should().Be("7811");
        row.SyncStatus.Should().Be(TicketSyncStatus.Synced);
        row.SyncError.Should().BeNull();
        (await Service(h, stub).ListAsync()).Count.Should().Be(0);
    }

    [Fact]
    public async Task Resync_applies_the_field_mappings_in_force_now()
    {
        var h = await SeedAsync(defaultQueue: "1");
        await using var _ = h.Db;
        h.Db.FieldMappings.Add(new FieldMapping
        {
            MspOrganizationId = Org, Provider = ProviderType.AutotaskPsa, PsaConnectionId = Conn,
            Scope = MappingScope.ConnectionOverride, PortalField = "priority", PortalValue = "HIGH",
            ExternalField = "priority", ExternalValue = "1", Direction = MappingDirection.Bidirectional,
        });
        var ticket = Unsynced("Priority was not mapped when this failed");
        h.Db.Tickets.Add(ticket);
        await h.Db.SaveChangesAsync();

        var stub = new StubConnector();
        await Service(h, stub).ResyncAsync(ticket.Id);

        // Autotask needs a numeric priority; the rule added after the failure is what supplies it.
        stub.CreateRequests.Should().ContainSingle().Which.Priority.Should().Be("1");
    }

    [Fact]
    public async Task A_failed_resync_keeps_the_ticket_listed_with_the_new_reason()
    {
        var h = await SeedAsync();
        await using var _ = h.Db;
        var ticket = Unsynced("Still broken");
        h.Db.Tickets.Add(ticket);
        await h.Db.SaveChangesAsync();

        var stub = new StubConnector { NextCreateResult = new CreateTicketResult(false, null, "companyID is required") };
        var result = await Service(h, stub).ResyncAsync(ticket.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("companyID is required");
        var listed = await Service(h, stub).ListAsync();
        listed.Count.Should().Be(1);
        listed.Tickets[0].SyncError.Should().Be("companyID is required"); // the newest reason, not the stale one
    }

    [Fact]
    public async Task Resyncing_an_already_synced_ticket_does_not_create_a_duplicate()
    {
        var h = await SeedAsync();
        await using var _ = h.Db;
        var t = Unsynced("Already there", error: null);
        t.ExternalTicketId = "7810";
        t.SyncStatus = TicketSyncStatus.Synced;
        h.Db.Tickets.Add(t);
        await h.Db.SaveChangesAsync();

        var stub = new StubConnector();
        var result = await Service(h, stub).ResyncAsync(t.Id);

        result.Success.Should().BeTrue();
        result.ExternalTicketId.Should().Be("7810");
        stub.CreateRequests.Should().BeEmpty(); // the provider was never asked to create a second one
    }
}
