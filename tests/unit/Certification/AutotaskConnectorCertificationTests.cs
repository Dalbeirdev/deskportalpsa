using System.Net;
using Desk.Connectors.Autotask;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit.Certification;

/// <summary>
/// Runs the shared certification suite against the REAL AutotaskConnector, driven by an in-memory
/// fake Autotask server. This certifies request construction, JSON parsing, and HTTP error mapping
/// end-to-end. Same tests as the mock — proving cross-provider contract compliance.
/// </summary>
public sealed class AutotaskConnectorCertificationTests : ConnectorCertificationSuite
{
    private const string Secret = "at-webhook-secret";

    private AutotaskConnector Build(FakeAutotaskServer server)
    {
        var http = new HttpClient(server) { BaseAddress = new Uri("https://webservices.local/atservicesrest/") };
        var config = new AutotaskConnectorConfig
        {
            BaseUrl = "https://webservices.local/atservicesrest/",
            Credentials = new AutotaskCredentials("code", "user", "secret"),
            WebhookSecret = Secret,
        };
        return new AutotaskConnector(http, config, Clock);
    }

    protected override IServiceManagementConnector CreateConnector() => Build(new FakeAutotaskServer(Clock));

    protected override IServiceManagementConnector CreateFailingConnector(ConnectorFailureKind kind)
    {
        var status = kind switch
        {
            ConnectorFailureKind.Authentication => HttpStatusCode.Unauthorized,
            ConnectorFailureKind.PermissionDenied => HttpStatusCode.Forbidden,
            ConnectorFailureKind.RateLimited => (HttpStatusCode)429,
            _ => HttpStatusCode.InternalServerError,
        };
        return Build(new FakeAutotaskServer(Clock) { ForceStatus = status });
    }

    protected override string SeededOrganizationId => "1";

    /// <summary>
    /// The live failure this pins: changing a ticket's status from the portal sent the LABEL
    /// ("In Progress") into a numeric picklist field, and Autotask answered
    /// HTTP 500 "Could not convert string to integer". Labels must resolve to the tenant's own
    /// picklist ids on the way out — and ids must resolve back to labels on the way in, or the
    /// portal shows the user a status of "1".
    /// </summary>
    [Fact]
    public async Task Picklist_labels_resolve_to_ids_outbound_and_back_to_labels_inbound()
    {
        var c = CreateConnector();
        var created = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "picklist round trip",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            ExternalCompanyId = SeededOrganizationId,
            Status = "New",              // label, not an id
            Priority = "High",
            QueueOrBoard = "Service Desk",
        });
        created.Success.Should().BeTrue("a label must not reach Autotask as a string");

        // Inbound: the provider holds numeric ids; the portal must receive the tenant's labels.
        var read = await c.GetTicketAsync(created.ExternalId!);
        read!.Status.Should().Be("New");
        read.Priority.Should().Be("High");
        read.QueueOrBoard.Should().Be("Service Desk");

        // Outbound update, the exact path that failed in production.
        var updated = await c.UpdateTicketAsync(created.ExternalId!,
            new UnifiedTicketUpdate { Status = "Complete", IdempotencyKey = "k1" });
        updated.Success.Should().BeTrue();
        (await c.GetTicketAsync(created.ExternalId!))!.Status.Should().Be("Complete");

        // An id given directly still works — mappings that already store ids must keep working.
        (await c.UpdateTicketAsync(created.ExternalId!,
            new UnifiedTicketUpdate { Status = "1", IdempotencyKey = "k2" })).Success.Should().BeTrue();
        (await c.GetTicketAsync(created.ExternalId!))!.Status.Should().Be("New");
    }

    /// <summary>
    /// The live failure this pins: an admin configured time-entry technician "Autotask
    /// Administrator" with work role "Help Desk" — a pairing that resource does not hold — and
    /// every entry came back HTTP 500 "The specified AssignedResourceID and AssignedRoleID
    /// combination is not currently defined". Autotask does not accept a technician and a role
    /// independently; the PAIR must exist. The connector must use a role the resource actually
    /// holds rather than trusting a configured one that can never work.
    /// </summary>
    [Fact]
    public async Task A_configured_role_the_technician_does_not_hold_is_corrected_to_one_they_do()
    {
        var server = new FakeAutotaskServer(Clock);
        // Resource 20 holds role 55 only. The connection is configured with role 999 — plausible
        // to a human reading a role list, impossible for Autotask.
        var c = BuildWithTimeDefaults(server, resourceId: 20, roleId: 999);

        var created = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "time", IdempotencyKey = "k", ExternalCompanyId = SeededOrganizationId,
        });
        var result = await c.AddTimeEntryAsync(created.ExternalId!,
            new UnifiedTimeEntryCreateRequest(1m, null, null, BillableOption.Billable, "ok good work team", null));

        result.Success.Should().BeTrue("a valid pairing exists and must be used rather than losing the entry");
        server.TimeEntries.Should().ContainSingle()
            .Which["roleID"].Should().Be(55L, "the role the resource actually holds, not the configured one");
    }

    /// <summary>
    /// The readiness check exists so a misconfiguration is found on the settings page instead of
    /// when a technician's logged hour is rejected. It must name the technician, name the roles
    /// they really hold, and never write anything.
    /// </summary>
    [Fact]
    public async Task Time_entry_readiness_reports_the_real_pairing_without_writing_an_entry()
    {
        var server = new FakeAutotaskServer(Clock);

        // Configured with a role the technician does not hold — the live situation.
        var bad = await BuildWithTimeDefaults(server, resourceId: 20, roleId: 999).CheckTimeEntryReadinessAsync();
        bad.Ready.Should().BeFalse();
        bad.Summary.Should().Contain("Tech One").And.Contain("does not hold");
        bad.Remedies.Should().NotBeEmpty();
        bad.AvailableRoles.Should().NotBeEmpty("the admin needs the real options to choose from");

        // Configured correctly.
        var good = await BuildWithTimeDefaults(server, resourceId: 20, roleId: 55).CheckTimeEntryReadinessAsync();
        good.Ready.Should().BeTrue();
        good.Summary.Should().Contain("Tech One");

        // Nothing configured at all.
        var none = await BuildWithTimeDefaults(server, resourceId: 0, roleId: null).CheckTimeEntryReadinessAsync();
        none.Ready.Should().BeFalse();
        none.Summary.Should().Contain("No time-entry technician");

        server.TimeEntries.Should().BeEmpty("a readiness CHECK must never create a time entry");
    }

    /// <summary>
    /// The live failure this pins: 15 minutes logged with no notes came back
    /// "TimeEntry.summaryNotes can not be blank." Autotask mandates the field; ConnectWise does
    /// not, so the portal accepted the entry and the PSA refused it. The technician's time must
    /// survive the difference.
    /// </summary>
    [Fact]
    public async Task Time_logged_without_notes_still_reaches_autotask()
    {
        var server = new FakeAutotaskServer(Clock);
        var c = BuildWithTimeDefaults(server, resourceId: 20, roleId: 55);
        var created = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "t", IdempotencyKey = "k", ExternalCompanyId = SeededOrganizationId,
        });

        var result = await c.AddTimeEntryAsync(created.ExternalId!,
            new UnifiedTimeEntryCreateRequest(0.25m, null, null, BillableOption.Billable, "   ", null));

        result.Success.Should().BeTrue("blank notes are a portal-side gap, not a reason to lose the time");
        server.TimeEntries.Should().ContainSingle()
            .Which["summaryNotes"].ToString().Should().NotBeNullOrWhiteSpace();

        // Real notes must still travel through untouched.
        await c.AddTimeEntryAsync(created.ExternalId!,
            new UnifiedTimeEntryCreateRequest(0.5m, null, null, BillableOption.Billable, "Rebuilt the mail profile.", null));
        server.TimeEntries[1]["summaryNotes"].Should().Be("Rebuilt the mail profile.");
    }

    /// <summary>When the resource holds no role at all, say so — that is a real Autotask setup gap.</summary>
    [Fact]
    public async Task A_technician_with_no_active_role_is_reported_as_the_setup_problem_it_is()
    {
        var server = new FakeAutotaskServer(Clock);
        server.ResourceRoles.Clear();
        var c = BuildWithTimeDefaults(server, resourceId: 20, roleId: null);

        var created = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "time", IdempotencyKey = "k", ExternalCompanyId = SeededOrganizationId,
        });
        var act = async () => await c.AddTimeEntryAsync(created.ExternalId!,
            new UnifiedTimeEntryCreateRequest(1m, null, null, BillableOption.Billable, "n", null));

        (await act.Should().ThrowAsync<ConnectorException>()).Which.Message
            .Should().Contain("holds no active work role");
    }

    private AutotaskConnector BuildWithTimeDefaults(FakeAutotaskServer server, long resourceId, long? roleId)
    {
        var http = new HttpClient(server) { BaseAddress = new Uri("https://at.local/ATServicesRest/") };
        return new AutotaskConnector(http, new AutotaskConnectorConfig
        {
            BaseUrl = "https://at.local/ATServicesRest/",
            Credentials = new AutotaskCredentials("code", "user", "secret"),
            WebhookSecret = Secret,
            DefaultTimeEntryResourceId = resourceId,
            DefaultTimeEntryRoleId = roleId,
        }, Clock);
    }

    /// <summary>
    /// A portal status with no Autotask counterpart must say WHAT to do. Autotask's own answer is
    /// HTTP 500 "Could not convert string to integer", which tells the reader nothing; the connector
    /// names the tenant's real options instead, because the fix is a field mapping.
    /// </summary>
    [Fact]
    public async Task An_unmappable_status_names_the_tenants_real_options()
    {
        var c = CreateConnector();
        var created = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "t", IdempotencyKey = "k", ExternalCompanyId = SeededOrganizationId,
        });

        var act = async () => await c.UpdateTicketAsync(created.ExternalId!,
            new UnifiedTicketUpdate { Status = "Awaiting Parts From Vendor", IdempotencyKey = "k2" });

        var ex = (await act.Should().ThrowAsync<ConnectorException>()).Which;
        ex.Kind.Should().Be(ConnectorFailureKind.InvalidRequest);
        ex.Message.Should().Contain("New").And.Contain("Complete", "the reader needs the real options to map onto");
        ex.Message.Should().NotContain("convert string to integer", "that is the message this replaces");
    }
    /// <summary>
    /// Autotask prints a note's title above its description, so a title taken as 250 characters of
    /// the body made one note read as two — the opening paragraph in bold, then again below.
    /// Reported from production. The title must be a heading, not a copy of the paragraph under it.
    /// </summary>
    [Fact]
    public async Task A_long_note_gets_a_short_heading_not_a_second_copy_of_itself()
    {
        var server = new FakeAutotaskServer(Clock);
        var c = Build(server);
        var ticket = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "t", IdempotencyKey = "k", ExternalCompanyId = SeededOrganizationId,
        });
        const string body =
            "we already testing every task from my side didn't surface the dead-letter count in the UI. "
            + "The state exists and is countable, but nothing displays it yet, so an operator cannot "
            + "currently see that OCR has given up on N screenshots without querying.";

        await c.AddPublicNoteAsync(ticket.ExternalId!, new UnifiedTicketNoteCreateRequest(body, IsPublic: true, "k2"));

        var title = server.LastNoteTitle!;
        title.Length.Should().BeLessThanOrEqualTo(81, "a heading, not a paragraph (80 chars plus the ellipsis)");
        title.Should().EndWith("…");
        title.Should().NotEndWith(" …", "the boundary trim should not leave a dangling space");
        // Cut on a word boundary: the last word before the ellipsis must be whole.
        body.Should().Contain(title.TrimEnd('…'), "the heading is an excerpt, not a re-wording");
        title.TrimEnd('…').Should().NotEndWith("didn'", "a mid-word cut is what made it look like broken duplicate text");
    }

    [Fact]
    public async Task A_short_note_is_its_own_title_unchanged()
    {
        var server = new FakeAutotaskServer(Clock);
        var c = Build(server);
        var ticket = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "t", IdempotencyKey = "k", ExternalCompanyId = SeededOrganizationId,
        });

        await c.AddPublicNoteAsync(ticket.ExternalId!,
            new UnifiedTicketNoteCreateRequest("Rebooted the switch, link is up.", IsPublic: true, "k2"));

        server.LastNoteTitle.Should().Be("Rebooted the switch, link is up.",
            "nothing to shorten, so nothing should be altered");
    }

    protected override string WebhookSecret => Secret;
}
