using System.Text.Json;
using Desk.Connectors.ConnectWise;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// What ConnectWise actually sends on a ticket, pinned against a captured payload.
///
/// Every date bug here came from assuming a field name and reading null when it did not match — and
/// a null is indistinguishable from a ticket that genuinely has no date, so the mistake looks
/// exactly like correct behaviour. These fix the shape so a rename shows up as a failing test
/// rather than as an empty column somebody notices months later.
/// </summary>
public class ConnectWisePayloadTests
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    // Trimmed from a live response. The parts that matter: the raise date lives under "_info", not
    // at the top level, and neither closedDate nor requiredDate is present at all.
    private const string OpenTicketJson = """
    {
      "id": 4321,
      "summary": "Printer offline",
      "closedFlag": false,
      "resolutionGoalUTC": "2026-09-05T14:00:00Z",
      "respondByGoalUTC": "2026-09-03T10:00:00Z",
      "_info": {
        "dateEntered": "2026-09-01T08:30:00Z",
        "lastUpdated": "2026-09-02T11:00:00Z",
        "enteredBy": "someone"
      }
    }
    """;

    private static CwTicket Parse(string json) => JsonSerializer.Deserialize<CwTicket>(json, Opts)!;

    [Fact]
    public void The_raise_date_is_read_from_the_info_block()
    {
        // The top-level "dateEntered" that seems obvious is not sent; "_info.dateEntered" is. Bound
        // by an explicit attribute, because case-insensitive matching maps "info" and not "_info".
        Parse(OpenTicketJson).RaisedAt
            .Should().Be(new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void The_sla_target_falls_back_to_the_resolution_goal()
    {
        // ConnectWise omits null fields, and across a whole board no ticket carried a requiredDate
        // while every one carried resolutionGoalUTC. Reading only requiredDate left the SLA column
        // empty on every ticket, which read as "no SLA" rather than "wrong field".
        Parse(OpenTicketJson).SlaTargetAt
            .Should().Be(new DateTimeOffset(2026, 9, 5, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_date_a_human_set_wins_over_the_sla_goal()
    {
        var json = OpenTicketJson.Replace(
            "\"resolutionGoalUTC\"", "\"requiredDate\": \"2026-09-04T09:00:00Z\", \"resolutionGoalUTC\"");

        Parse(json).SlaTargetAt
            .Should().Be(new DateTimeOffset(2026, 9, 4, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void An_open_ticket_has_no_closure_date_rather_than_a_guessed_one()
    {
        // The field is absent on an open ticket. Nothing in the payload stands in for it, and
        // reaching for lastUpdated would put a plausible wrong timestamp in a column people measure
        // resolution time with.
        Parse(OpenTicketJson).ClosedAtAny.Should().BeNull();
    }

    [Fact]
    public void A_closed_ticket_carries_its_closure_date()
    {
        var json = OpenTicketJson.Replace(
            "\"closedFlag\": false", "\"closedFlag\": true, \"closedDate\": \"2026-09-02T16:45:00Z\"");

        Parse(json).ClosedAtAny
            .Should().Be(new DateTimeOffset(2026, 9, 2, 16, 45, 0, TimeSpan.Zero));
    }
}
