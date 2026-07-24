using Desk.Connectors.Mock;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The shared connector certification suite (Integration Plan §"Connector Testing Requirements").
/// Every provider connector must pass these; here they run against the reference MockConnector.
/// Real ConnectWise/Autotask connectors will be pointed at the same suite in phases 4-5.
/// </summary>
public class ConnectorCertificationTests
{
    private readonly TestClock _clock = new();

    private MockConnector Connector(MockConnectorOptions? opts = null) =>
        new(opts ?? new MockConnectorOptions(), _clock);

    [Fact]
    public async Task Declares_capabilities()
    {
        var caps = await Connector().GetCapabilitiesAsync();
        caps.SupportsTicketCreate.Should().BeTrue();
        caps.AuthenticationTypes.Should().NotBeEmpty();
        caps.MaximumPageSize.Should().BePositive();
    }

    [Fact]
    public async Task Test_connection_succeeds()
        => (await Connector().TestConnectionAsync()).Success.Should().BeTrue();

    [Fact]
    public async Task Invalid_credentials_surface_as_authentication_failure()
    {
        var c = Connector(new MockConnectorOptions { FailEveryCallWith = ConnectorFailureKind.Authentication });
        var act = async () => await c.TestConnectionAsync();
        (await act.Should().ThrowAsync<ConnectorException>()).Which.Kind.Should().Be(ConnectorFailureKind.Authentication);
    }

    [Fact]
    public async Task Permission_denied_is_reported()
    {
        var c = Connector(new MockConnectorOptions { FailEveryCallWith = ConnectorFailureKind.PermissionDenied });
        var act = async () => await c.GetOrganizationsAsync();
        (await act.Should().ThrowAsync<ConnectorException>()).Which.Kind.Should().Be(ConnectorFailureKind.PermissionDenied);
    }

    [Fact]
    public async Task Rate_limit_is_transient_with_retry_after()
    {
        var c = Connector(new MockConnectorOptions { FailEveryCallWith = ConnectorFailureKind.RateLimited });
        var ex = (await ((Func<Task>)(() => c.TestConnectionAsync())).Should().ThrowAsync<ConnectorException>()).Which;
        ex.IsTransient.Should().BeTrue();
        ex.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Directory_sync_returns_orgs_contacts_and_technicians()
    {
        var c = Connector();
        (await c.GetOrganizationsAsync()).Should().NotBeEmpty();
        (await c.GetContactsAsync("ORG-1")).Should().NotBeEmpty();
        (await c.GetTechniciansAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Ticket_create_read_update_round_trips()
    {
        var c = Connector();
        var created = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "Printer down", ExternalCompanyId = "ORG-1", IdempotencyKey = "k1", Priority = "High",
        });
        created.Success.Should().BeTrue();
        created.ExternalId.Should().NotBeNull();

        var read = await c.GetTicketAsync(created.ExternalId!);
        read!.Title.Should().Be("Printer down");

        var upd = await c.UpdateTicketAsync(created.ExternalId!, new UnifiedTicketUpdate { Status = "Resolved", IdempotencyKey = "k2" });
        upd.Success.Should().BeTrue();
        (await c.GetTicketAsync(created.ExternalId!))!.Status.Should().Be("Resolved");
    }

    [Fact]
    public async Task Create_is_idempotent_on_the_idempotency_key()
    {
        var c = Connector();
        var req = new UnifiedTicketCreateRequest { Title = "Dup", ExternalCompanyId = "ORG-1", IdempotencyKey = "same-key" };
        var first = await c.CreateTicketAsync(req);
        var second = await c.CreateTicketAsync(req);
        second.ExternalId.Should().Be(first.ExternalId); // no duplicate ticket
    }

    [Fact]
    public async Task Public_note_is_added_and_only_public_notes_are_returned()
    {
        var c = Connector();
        var t = await c.CreateTicketAsync(new UnifiedTicketCreateRequest { Title = "T", ExternalCompanyId = "ORG-1", IdempotencyKey = "n1" });
        await c.AddPublicNoteAsync(t.ExternalId!, new UnifiedTicketNoteCreateRequest("hello", IsPublic: true, "note-key"));
        var notes = await c.GetPublicNotesAsync(t.ExternalId!);
        notes.Should().ContainSingle().Which.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Update_of_missing_ticket_is_not_found()
    {
        var c = Connector();
        var act = async () => await c.UpdateTicketAsync("T-does-not-exist", new UnifiedTicketUpdate { IdempotencyKey = "x" });
        (await act.Should().ThrowAsync<ConnectorException>()).Which.Kind.Should().Be(ConnectorFailureKind.NotFound);
    }

    [Fact]
    public async Task Field_options_are_retrieved_live()
    {
        var c = Connector();
        (await c.GetStatusesAsync()).Should().NotBeEmpty();
        (await c.GetPrioritiesAsync()).Should().NotBeEmpty();
        (await c.GetQueuesOrBoardsAsync()).Should().NotBeEmpty();
        (await c.GetCategoriesAsync()).Should().NotBeEmpty();
        (await c.GetCustomFieldsAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Incremental_read_filters_by_modified_since()
    {
        var c = Connector();
        await c.CreateTicketAsync(new UnifiedTicketCreateRequest { Title = "old", ExternalCompanyId = "ORG-1", IdempotencyKey = "old" });
        _clock.Advance(TimeSpan.FromHours(1));
        var cutoff = _clock.GetUtcNow();
        _clock.Advance(TimeSpan.FromMinutes(1));
        await c.CreateTicketAsync(new UnifiedTicketCreateRequest { Title = "new", ExternalCompanyId = "ORG-1", IdempotencyKey = "new" });

        var page = await c.GetTicketsAsync(new TicketFilter { ModifiedSince = cutoff });
        page.Items.Should().OnlyContain(t => t.Title == "new");
    }

    // ---- Webhook validation ----

    private WebhookRequest SignedWebhook(string body, MockConnectorOptions opts, DateTimeOffset? ts = null)
    {
        var stamp = ts ?? _clock.GetUtcNow();
        return new WebhookRequest(
            Headers: new Dictionary<string, string> { ["X-Timestamp"] = stamp.ToString("o") },
            Body: body,
            RawSignature: MockConnector.SignBody(body, opts.WebhookSecret),
            ReceivedAt: _clock.GetUtcNow());
    }

    [Fact]
    public async Task Valid_webhook_signature_passes()
    {
        var opts = new MockConnectorOptions();
        var c = Connector(opts);
        var result = await c.ValidateWebhookAsync(SignedWebhook("{\"eventType\":\"ticket.updated\"}", opts));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Tampered_signature_is_rejected()
    {
        var opts = new MockConnectorOptions();
        var c = Connector(opts);
        var req = SignedWebhook("{\"x\":1}", opts) with { RawSignature = "deadbeef" };
        (await c.ValidateWebhookAsync(req)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Stale_timestamp_is_rejected_replay_protection()
    {
        var opts = new MockConnectorOptions();
        var c = Connector(opts);
        var stale = _clock.GetUtcNow() - TimeSpan.FromMinutes(30);
        (await c.ValidateWebhookAsync(SignedWebhook("{\"x\":1}", opts, stale))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_payload_normalizes_to_a_provider_event()
    {
        var c = Connector();
        var body = "{\"eventType\":\"ticket.updated\",\"ticketId\":\"T-1001\",\"id\":\"evt-9\"}";
        var evt = await c.ProcessWebhookAsync(new WebhookRequest(new Dictionary<string, string>(), body, null, _clock.GetUtcNow()));
        evt.EventType.Should().Be("ticket.updated");
        evt.ExternalTicketId.Should().Be("T-1001");
        evt.IdempotencyKey.Should().Be("evt-9");
    }
}
