using System.Net;
using Desk.Connectors.ConnectWise;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit.Certification;

/// <summary>
/// Runs the shared certification suite against the REAL ConnectWiseConnector via an in-memory fake
/// ConnectWise server. Passing the same suite as the mock and Autotask connectors is the
/// cross-provider contract-compliance proof.
/// </summary>
public sealed class ConnectWiseConnectorCertificationTests : ConnectorCertificationSuite
{
    private const string Secret = "cw-webhook-secret";

    private ConnectWiseConnector Build(FakeConnectWiseServer server)
    {
        var http = new HttpClient(server) { BaseAddress = new Uri("https://cw.local/v4_6_release/apis/3.0/") };
        var config = new ConnectWiseConnectorConfig
        {
            BaseUrl = "https://cw.local/v4_6_release/apis/3.0/",
            Credentials = new ConnectWiseCredentials("acme", "pub", "priv", "client-guid"),
            WebhookSecret = Secret,
        };
        return new ConnectWiseConnector(http, config, Clock);
    }

    protected override IServiceManagementConnector CreateConnector() => Build(new FakeConnectWiseServer(Clock));

    protected override IServiceManagementConnector CreateFailingConnector(ConnectorFailureKind kind)
    {
        var status = kind switch
        {
            ConnectorFailureKind.Authentication => HttpStatusCode.Unauthorized,
            ConnectorFailureKind.PermissionDenied => HttpStatusCode.Forbidden,
            ConnectorFailureKind.RateLimited => (HttpStatusCode)429,
            _ => HttpStatusCode.InternalServerError,
        };
        return Build(new FakeConnectWiseServer(Clock) { ForceStatus = status });
    }

    protected override string SeededOrganizationId => "1";
    protected override string WebhookSecret => Secret;

    /// <summary>
    /// The live finding this pins: a customer contact's notes rendered as the MSP's own words.
    /// Side detection must follow ATTRIBUTION — whichever of member/contact supplies the display
    /// name — because CW can return an empty member stub (no name) on contact-authored notes, so
    /// checking only `member != null` claims every note for the MSP.
    /// </summary>
    [Fact]
    public async Task Note_author_side_follows_whoever_supplies_the_name()
    {
        var server = new FakeConnectWiseServer(Clock);
        server.SeedNote(777, new() // technician note: member named
        {
            ["text"] = "Working on it.",
            ["member"] = new Dictionary<string, object?> { ["id"] = 20L, ["name"] = "Tech One" },
        });
        server.SeedNote(777, new() // customer portal note: contact named, no member
        {
            ["text"] = "Still broken.",
            ["contact"] = new Dictionary<string, object?> { ["id"] = 10L, ["name"] = "Harpal Singh" },
        });
        server.SeedNote(777, new() // the live quirk: empty member stub alongside the real contact
        {
            ["text"] = "Broken again.",
            ["member"] = new Dictionary<string, object?> { ["id"] = 0L },
            ["contact"] = new Dictionary<string, object?> { ["id"] = 10L, ["name"] = "Harpal Singh" },
        });
        var c = Build(server);

        var notes = await c.GetNotesAsync("777");

        notes.Should().HaveCount(3);
        notes[0].FromClient.Should().BeFalse("a named member wrote it");
        notes[0].AuthorName.Should().Be("Tech One");
        notes[1].FromClient.Should().BeTrue("the contact is the only author CW names");
        notes[1].AuthorName.Should().Be("Harpal Singh");
        notes[2].FromClient.Should().BeTrue("an empty member stub is not an author — the named contact is");
        notes[2].AuthorName.Should().Be("Harpal Singh");
    }

    /// <summary>
    /// CW validates status against the ticket's BOARD on create. A mapped status is typically the
    /// verbose global name ("New (not responded)") while a board names it tersely ("New") — the
    /// connector must resolve one to the other, or every portal create on that board fails.
    /// </summary>
    [Fact]
    public async Task Create_resolves_a_verbose_status_against_the_boards_own_names()
    {
        var c = CreateConnector();
        var created = await c.CreateTicketAsync(new UnifiedTicketCreateRequest
        {
            Title = "board-scoped status",
            ExternalCompanyId = SeededOrganizationId,
            QueueOrBoard = "1",
            Status = "New (not responded)", // no board carries this literal name
            IdempotencyKey = "status-resolve",
        });

        created.Success.Should().BeTrue();
        (await c.GetTicketAsync(created.ExternalId!))!.Status.Should().NotBeNull();
    }

    /// <summary>
    /// A rejection whose body is not the documented {message, errors[]} shape must still reach the
    /// admin. Live ConnectWise answered a bad connection with plain text, the parser returned null,
    /// and the UI showed only "ConnectWise rejected the request (400)" — a status code the admin
    /// cannot act on, while the provider's actual reason was in hand the whole time.
    /// </summary>
    [Theory]
    [InlineData("Invalid or missing clientId")]           // plain text, not JSON at all
    [InlineData("{\"code\":\"InvalidCredentials\"}")]     // JSON, but no message/errors member
    public async Task A_rejection_body_in_an_unexpected_shape_still_reaches_the_admin(string body)
    {
        var c = Build(new FakeConnectWiseServer(Clock) { ForceStatus = HttpStatusCode.BadRequest, ForceBody = body });

        var act = async () => await c.TestConnectionAsync();

        (await act.Should().ThrowAsync<ConnectorException>())
            .Which.Message.Should().Contain(body);
    }
}
