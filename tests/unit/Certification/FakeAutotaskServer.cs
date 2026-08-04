using System.Net;
using System.Text;
using System.Text.Json;
using Desk.PsaCore.Contracts;

namespace Desk.Tests.Unit.Certification;

/// <summary>
/// In-memory HttpMessageHandler that emulates the Autotask REST v1.0 dialect with real state
/// (companies, contacts, resources, tickets, notes, picklists). Lets the real AutotaskConnector be
/// certified end-to-end — request construction, JSON parsing, error mapping — without a live sandbox.
/// Can be rigged to fail every call with a specific HTTP status for the error-mapping tests.
/// </summary>
public sealed class FakeAutotaskServer(TimeProvider clock) : HttpMessageHandler
{
    public HttpStatusCode? ForceStatus { get; set; }

    private long _seq = 100;
    private readonly List<Dictionary<string, object?>> _companies =
        [new() { ["id"] = 1L, ["companyName"] = "Acme Corp", ["isActive"] = true }];
    private readonly List<Dictionary<string, object?>> _contacts =
        [new() { ["id"] = 10L, ["companyID"] = 1L, ["emailAddress"] = "user@acme.test", ["firstName"] = "Acme", ["lastName"] = "User", ["isActive"] = true }];
    private readonly List<Dictionary<string, object?>> _resources =
        [new() { ["id"] = 20L, ["email"] = "tech@msp.test", ["firstName"] = "Tech", ["lastName"] = "One", ["isActive"] = true }];
    private readonly List<Dictionary<string, object?>> _tickets = [];
    private readonly List<Dictionary<string, object?>> _notes = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (ForceStatus is { } forced)
            return Resp(forced, "{\"errors\":[\"forced\"]}");

        // Credentials must be present on every call (auth surface).
        if (!request.Headers.Contains("ApiIntegrationCode") || !request.Headers.Contains("Secret"))
            return Resp(HttpStatusCode.Unauthorized, "{}");

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);

        // Picklist field info
        if (path.EndsWith("Tickets/entityInformation/fields", StringComparison.OrdinalIgnoreCase))
            return Json(FieldInfoJson());
        if (path.EndsWith("entityInformation/userDefinedFields", StringComparison.OrdinalIgnoreCase))
            return Json("{\"fields\":[{\"name\":\"cf_site\",\"picklistValues\":[]}]}");

        // Query endpoints
        if (path.EndsWith("Companies/query", StringComparison.OrdinalIgnoreCase)) return Json(QueryJson(_companies, body));
        if (path.EndsWith("Contacts/query", StringComparison.OrdinalIgnoreCase)) return Json(QueryJson(_contacts, body));
        if (path.EndsWith("Resources/query", StringComparison.OrdinalIgnoreCase)) return Json(QueryJson(_resources, body));
        if (path.EndsWith("Tickets/query", StringComparison.OrdinalIgnoreCase)) return Json(QueryJson(_tickets, body));
        if (path.EndsWith("TicketNotes/query", StringComparison.OrdinalIgnoreCase)) return Json(QueryJson(_notes, body));

        // Create / update
        if (path.EndsWith("V1.0/Tickets", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            return Json(CreateTicket(body));
        if (path.EndsWith("V1.0/Tickets", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Patch)
            return UpdateTicket(body);
        // Notes are a CHILD collection: creates go to the parent ticket's /Notes route. Posting to
        // the top-level TicketNotes entity 404s, exactly as the live API does.
        if (path.EndsWith("/Notes", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            return CreateNote(body);
        if (path.EndsWith("V1.0/TicketNotes", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            return Resp(HttpStatusCode.NotFound, "{\"errors\":[\"entity not found\"]}");
        if (path.EndsWith("V1.0/TicketAttachments", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            return Json($"{{\"itemId\":{++_seq}}}");

        // Get by id: .../V1.0/Tickets/{id}
        if (path.Contains("V1.0/Tickets/", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
        {
            var id = long.Parse(path[(path.LastIndexOf('/') + 1)..]);
            var t = _tickets.FirstOrDefault(x => (long)x["id"]! == id);
            return t is null ? Resp(HttpStatusCode.NotFound, "{}") : Json("{\"item\":" + Serialize(t) + "}");
        }

        return Resp(HttpStatusCode.NotFound, "{}");
    }

    private string CreateTicket(string body)
    {
        var input = Parse(body);
        var now = clock.GetUtcNow().ToString("o");
        var ticket = new Dictionary<string, object?>
        {
            ["id"] = ++_seq,
            ["title"] = input.GetValueOrDefault("title"),
            ["description"] = input.GetValueOrDefault("description"),
            ["status"] = input.GetValueOrDefault("status"),
            ["priority"] = input.GetValueOrDefault("priority"),
            ["queueID"] = input.GetValueOrDefault("queueID"),
            ["ticketCategory"] = input.GetValueOrDefault("ticketCategory"),
            ["companyID"] = input.GetValueOrDefault("companyID"),
            ["createDate"] = now,
            ["lastActivityDate"] = now,
        };
        _tickets.Add(ticket);
        return $"{{\"itemId\":{ticket["id"]}}}";
    }

    private HttpResponseMessage UpdateTicket(string body)
    {
        var input = Parse(body);
        var id = Convert.ToInt64(input["id"]);
        var ticket = _tickets.FirstOrDefault(x => (long)x["id"]! == id);
        if (ticket is null) return Resp(HttpStatusCode.NotFound, "{}");
        foreach (var (k, v) in input)
            if (k != "id") ticket[k] = v;
        ticket["lastActivityDate"] = clock.GetUtcNow().ToString("o");
        return Json($"{{\"itemId\":{id}}}");
    }

    private HttpResponseMessage CreateNote(string body)
    {
        var input = Parse(body);
        // The live API rejects a note without a title; keep that contract so the connector's
        // title derivation stays covered.
        if (string.IsNullOrWhiteSpace(input.GetValueOrDefault("title")?.ToString()))
            return Resp(HttpStatusCode.BadRequest, "{\"errors\":[\"Missing Required Field: title\"]}");

        var note = new Dictionary<string, object?>
        {
            ["id"] = ++_seq,
            ["ticketID"] = Convert.ToInt64(input["ticketID"]),
            ["title"] = input.GetValueOrDefault("title"),
            ["description"] = input.GetValueOrDefault("description"),
            ["publish"] = Convert.ToInt32(input.GetValueOrDefault("publish") ?? 1),
            ["createDateTime"] = clock.GetUtcNow().ToString("o"),
            ["creatorResourceID"] = 20L, // the integration user, as Autotask stamps it
        };
        _notes.Add(note);
        return Json($"{{\"itemId\":{note["id"]}}}");
    }

    private sealed record FilterClause(string Field, string Op, long? Number, string? Text);

    // Minimal filter application: supports eq/gte on the fields the connector queries. Filter values
    // are extracted eagerly so no JsonElement is read after the JsonDocument is disposed.
    private static string QueryJson(List<Dictionary<string, object?>> rows, string body)
    {
        var clauses = new List<FilterClause>();
        if (!string.IsNullOrEmpty(body))
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("Filter", out var filters))
            {
                foreach (var f in filters.EnumerateArray())
                {
                    var val = f.GetProperty("value");
                    clauses.Add(new FilterClause(
                        f.GetProperty("field").GetString()!,
                        f.GetProperty("op").GetString()!,
                        val.ValueKind == JsonValueKind.Number ? val.GetInt64() : null,
                        val.ValueKind == JsonValueKind.String ? val.GetString() : null));
                }
            }
        }

        var result = rows.Where(r => clauses.All(c => Matches(r, c))).ToList();
        var items = string.Join(",", result.Select(Serialize));
        return $"{{\"items\":[{items}],\"pageDetails\":{{\"count\":{result.Count}}}}}";
    }

    private static bool Matches(Dictionary<string, object?> row, FilterClause c) => c.Op switch
    {
        "eq" => FieldEquals(row, c),
        "gte" => FieldGte(row, c),
        _ => true,
    };

    private static bool FieldEquals(Dictionary<string, object?> row, FilterClause c)
    {
        if (!row.TryGetValue(c.Field, out var actual) || actual is null) return false;
        return c.Number is { } n ? Convert.ToInt64(actual) == n : actual.ToString() == c.Text;
    }

    private static bool FieldGte(Dictionary<string, object?> row, FilterClause c)
    {
        if (c.Number is { } n) return Convert.ToInt64(row.GetValueOrDefault(c.Field) ?? 0L) >= n;
        if (row.TryGetValue(c.Field, out var actual) && actual is string s
            && DateTimeOffset.TryParse(s, out var d) && DateTimeOffset.TryParse(c.Text, out var since))
            return d >= since;
        return true;
    }

    private static string FieldInfoJson() =>
        "{\"fields\":[" +
        "{\"name\":\"status\",\"picklistValues\":[{\"value\":\"1\",\"label\":\"New\",\"isActive\":true},{\"value\":\"5\",\"label\":\"Complete\",\"isActive\":true}]}," +
        "{\"name\":\"priority\",\"picklistValues\":[{\"value\":\"1\",\"label\":\"High\",\"isActive\":true}]}," +
        "{\"name\":\"queueID\",\"picklistValues\":[{\"value\":\"8\",\"label\":\"Service Desk\",\"isActive\":true}]}]}";

    private static Dictionary<string, object?> Parse(string body) =>
        string.IsNullOrEmpty(body) ? [] :
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body)!
            .ToDictionary(kv => kv.Key, object? (kv) => kv.Value.ValueKind switch
            {
                JsonValueKind.Number => kv.Value.GetInt64(),
                JsonValueKind.String => kv.Value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            });

    private static string Serialize(Dictionary<string, object?> row) => JsonSerializer.Serialize(row);

    private static HttpResponseMessage Json(string json) => Resp(HttpStatusCode.OK, json);

    private static HttpResponseMessage Resp(HttpStatusCode code, string json) => new(code)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
