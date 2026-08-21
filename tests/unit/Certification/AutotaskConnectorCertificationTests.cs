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
    protected override string WebhookSecret => Secret;
}
