using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desk.Domain.Enums;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;

namespace Desk.Connectors.ConnectWise;

/// <summary>
/// ConnectWise Manage connector over REST API 3.0. Bound to one connection; credentials injected by
/// the factory (from Vault). Normalizes CW's terminology and nested {id,name} shapes into the same
/// unified models the Autotask connector produces — this is where cross-provider parity is realized.
///
/// Provider notes: CW nests references (status/priority/board/company) as objects; list endpoints
/// return bare JSON arrays; updates are JSON-Patch; "Service Board" maps to the portal's Queue and
/// "Member" to Technician. Public vs internal notes use the internalAnalysisFlag.
/// </summary>
public sealed class ConnectWiseConnector(HttpClient http, ConnectWiseConnectorConfig config, TimeProvider clock)
    : IServiceManagementConnector
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ProviderType Provider => ProviderType.ConnectWisePsa;

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct = default) =>
        Task.FromResult(new ProviderCapabilities
        {
            SupportsTicketCreate = true, SupportsTicketUpdate = true, SupportsTicketDelete = false,
            SupportsPublicNotes = true, SupportsPrivateNotes = true, SupportsAttachments = true,
            SupportsTimeEntries = true, SupportsAssets = true, SupportsContracts = true,
            SupportsSlaData = true, SupportsCustomFields = true, SupportsInboundWebhooks = true,
            SupportsOutboundWebhooks = true, SupportsIncrementalSync = true, SupportsBulkRead = true,
            SupportsBulkWrite = false, SupportsCompanies = true, SupportsContacts = true,
            SupportsTechnicians = true, SupportsTeams = true, SupportsQueues = true,
            MaximumPageSize = 1000, MaximumAttachmentSize = 60 * 1024 * 1024,
            RateLimitModel = "concurrent-request",
            AuthenticationTypes = [AuthenticationType.BasicAuth],
        });

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var start = clock.GetTimestamp();
        await GetListAsync<CwCompany>("company/companies", new() { ["pageSize"] = "1" }, ct);
        return new ConnectionTestResult(true, "OK", clock.GetElapsedTime(start));
    }

    public async Task<IReadOnlyList<ExternalOrganization>> GetOrganizationsAsync(CancellationToken ct = default)
    {
        var items = await GetListAsync<CwCompany>("company/companies", new() { ["pageSize"] = "1000" }, ct);
        return items.Select(c => new ExternalOrganization(c.Id.ToString(), c.Name ?? "", !c.DeletedFlag)).ToList();
    }

    public async Task<IReadOnlyList<ExternalContact>> GetContactsAsync(string organizationId, CancellationToken ct = default)
    {
        var items = await GetListAsync<CwContact>("company/contacts",
            new() { ["conditions"] = $"company/id={organizationId}", ["pageSize"] = "1000" }, ct);
        return items.Select(c => new ExternalContact(
            c.Id.ToString(), c.Email ?? "", $"{c.FirstName} {c.LastName}".Trim(), !c.InactiveFlag)).ToList();
    }

    public async Task<IReadOnlyList<ExternalTechnician>> GetTechniciansAsync(CancellationToken ct = default)
    {
        var items = await GetListAsync<CwMember>("system/members", new() { ["pageSize"] = "1000" }, ct);
        return items.Select(m => new ExternalTechnician(
            m.Id.ToString(), m.PrimaryEmail ?? "", $"{m.FirstName} {m.LastName}".Trim(), !m.InactiveFlag)).ToList();
    }

    public async Task<IReadOnlyList<ExternalDevice>> GetDevicesAsync(string organizationId, CancellationToken ct = default)
    {
        // CW calls managed assets "configurations"; they carry a type object and a serial/tag.
        var items = await GetListAsync<CwConfiguration>("company/configurations",
            new() { ["conditions"] = $"company/id={organizationId}", ["pageSize"] = "1000" }, ct);
        return items.Select(c => new ExternalDevice(
            c.Id.ToString(),
            c.Name ?? $"Configuration {c.Id}",
            c.Type?.Name,
            c.SerialNumber ?? c.TagNumber,
            !string.Equals(c.Status?.Name, "Inactive", StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public async Task<PaginatedResult<UnifiedTicket>> GetTicketsAsync(TicketFilter filter, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string> { ["pageSize"] = filter.PageSize.ToString() };
        var conditions = new List<string>();
        if (filter.ModifiedSince is { } since)
            conditions.Add($"lastUpdated>[{since.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}]");
        if (filter.ExternalCompanyId is { } company)
            conditions.Add($"company/id={company}");

        // Import filters. CW expresses "in" as an OR group over ids; closed state is closedFlag.
        if (filter.CompanyIds.Count > 0)
            conditions.Add(IdGroup("company/id", filter.CompanyIds));
        if (filter.QueueOrBoardIds.Count > 0)
            conditions.Add(IdGroup("board/id", filter.QueueOrBoardIds));
        if (filter.AssignedResourceIds.Count > 0)
            conditions.Add(IdGroup("owner/id", filter.AssignedResourceIds));
        if (!filter.IncludeClosed)
            conditions.Add("closedFlag=false");
        if (filter.ActiveWithinDays is > 0 and { } days)
            conditions.Add($"lastUpdated>[{clock.GetUtcNow().AddDays(-days):yyyy-MM-ddTHH:mm:ssZ}]");

        if (conditions.Count > 0)
            query["conditions"] = string.Join(" and ", conditions);

        var items = await GetListAsync<CwTicket>("service/tickets", query, ct);
        return new PaginatedResult<UnifiedTicket>(items.Select(ToUnified).ToList(), null, false);
    }

    public async Task<UnifiedTicket?> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        var t = await GetOneAsync<CwTicket>($"service/tickets/{ticketId}", ct);
        return t is null ? null : ToUnified(t);
    }

    public async Task<CreateTicketResult> CreateTicketAsync(UnifiedTicketCreateRequest ticket, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["summary"] = Truncate(ticket.Title, 100), // CW summary is capped at 100 chars
            ["initialDescription"] = ticket.Description,
            ["company"] = new { id = long.Parse(ticket.ExternalCompanyId) },
        };
        if (ticket.QueueOrBoard is not null) body["board"] = Ref(ticket.QueueOrBoard);
        if (ticket.Status is not null) body["status"] = Ref(ticket.Status);
        if (ticket.Priority is not null) body["priority"] = Ref(ticket.Priority);
        // CW's classification trio maps to the portal's ticket/issue/sub-issue types.
        if (ticket.TicketType is not null) body["type"] = Ref(ticket.TicketType);
        if (ticket.IssueType is not null) body["subType"] = Ref(ticket.IssueType);
        if (ticket.SubIssueType is not null) body["item"] = Ref(ticket.SubIssueType);

        var created = await SendAsync<CwTicket>(HttpMethod.Post, "service/tickets", body, ct);
        return new CreateTicketResult(true, created!.Id.ToString(), null);
    }

    public async Task<UpdateTicketResult> UpdateTicketAsync(string ticketId, UnifiedTicketUpdate update, CancellationToken ct = default)
    {
        var ticket = await GetOneAsync<CwTicket>($"service/tickets/{ticketId}", ct)
            ?? throw new ConnectorException(ConnectorFailureKind.NotFound, $"Ticket {ticketId} not found.");

        // ConnectWise updates are JSON-Patch operations replacing whole reference objects.
        var ops = new List<object>();
        if (update.Status is not null)
            ops.Add(new { op = "replace", path = "status", value = await ResolveBoardStatusAsync(ticket.Board?.Id, update.Status, ct) });
        if (update.Priority is not null) ops.Add(new { op = "replace", path = "priority", value = Ref(update.Priority) });
        if (update.QueueOrBoard is not null) ops.Add(new { op = "replace", path = "board", value = Ref(update.QueueOrBoard) });
        if (update.AssignedTechnicianExternalId is not null)
            ops.Add(new { op = "replace", path = "owner", value = Ref(update.AssignedTechnicianExternalId) });

        await SendAsync<CwTicket>(HttpMethod.Patch, $"service/tickets/{ticketId}", ops, ct);
        return new UpdateTicketResult(true, null);
    }

    public async Task<IReadOnlyList<UnifiedTicketNote>> GetPublicNotesAsync(string ticketId, CancellationToken ct = default)
    {
        var notes = await GetListAsync<CwTicketNote>($"service/tickets/{ticketId}/notes", new() { ["pageSize"] = "1000" }, ct);
        return notes.Where(n => !n.InternalAnalysisFlag)
            .Select(n => new UnifiedTicketNote(
                n.Id.ToString(), n.Member?.Name ?? "ConnectWise", n.Text ?? "", IsPublic: true, n.DateCreated ?? clock.GetUtcNow()))
            .ToList();
    }

    public async Task<CreateNoteResult> AddPublicNoteAsync(string ticketId, UnifiedTicketNoteCreateRequest note, CancellationToken ct = default)
    {
        var body = new
        {
            text = note.Body,
            detailDescriptionFlag = true,
            internalAnalysisFlag = !note.IsPublic, // public notes are not flagged internal
            customerUpdatedFlag = note.IsPublic,
        };
        var created = await SendAsync<CwTicketNote>(HttpMethod.Post, $"service/tickets/{ticketId}/notes", body, ct);
        return new CreateNoteResult(true, created!.Id.ToString(), null);
    }

    public Task<IReadOnlyList<UnifiedAttachment>> GetAttachmentsAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnifiedAttachment>>([]);

    public async Task<CreateAttachmentResult> AddAttachmentAsync(string ticketId, SecureAttachment attachment, CancellationToken ct = default)
    {
        var body = new
        {
            recordType = "Ticket",
            recordId = long.Parse(ticketId),
            title = attachment.FileName,
        };
        var created = await SendAsync<CwRef>(HttpMethod.Post, "system/documents", body, ct);
        return new CreateAttachmentResult(true, created!.Id.ToString(), null);
    }

    public async Task<IReadOnlyList<UnifiedTimeEntry>> GetTimeEntriesAsync(string ticketId, CancellationToken ct = default)
    {
        var items = await GetListAsync<CwTimeEntry>("time/entries",
            new() { ["conditions"] = $"chargeToId={ticketId} and chargeToType=\"ServiceTicket\"", ["pageSize"] = "1000" }, ct);
        return items.Select(e => new UnifiedTimeEntry(
            e.Id.ToString(), e.Member?.Id.ToString() ?? "", e.ActualHours ?? 0m,
            !string.Equals(e.BillableOption, "DoNotBill", StringComparison.OrdinalIgnoreCase),
            e.TimeStart ?? clock.GetUtcNow(), e.Notes)).ToList();
    }

    public async Task<CreateTimeEntryResult> AddTimeEntryAsync(string ticketId, UnifiedTimeEntryCreateRequest entry, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["chargeToId"] = long.Parse(ticketId),
            ["chargeToType"] = "ServiceTicket",
            ["timeStart"] = clock.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["actualHours"] = entry.Hours,
            ["billableOption"] = entry.Billable switch
            {
                BillableOption.DoNotBill => "DoNotBill",
                BillableOption.NoCharge => "NoCharge",
                _ => "Billable",
            },
            ["notes"] = entry.Notes,
        };
        if (entry.WorkType is not null) body["workType"] = Ref(entry.WorkType);
        if (entry.WorkRole is not null) body["workRole"] = Ref(entry.WorkRole);
        if (entry.MemberIdentifier is not null) body["member"] = new { identifier = entry.MemberIdentifier };

        var created = await SendAsync<CwRef>(HttpMethod.Post, "time/entries", body, ct);
        return new CreateTimeEntryResult(true, created!.Id.ToString(), null);
    }

    public async Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(string entryId, UnifiedTimeEntryUpdate update, CancellationToken ct = default)
    {
        var ops = new List<object>();
        if (update.Hours is { } h) ops.Add(new { op = "replace", path = "actualHours", value = h });
        if (update.Notes is not null) ops.Add(new { op = "replace", path = "notes", value = update.Notes });
        if (update.Billable is { } b)
            ops.Add(new { op = "replace", path = "billableOption", value = b switch { BillableOption.DoNotBill => "DoNotBill", BillableOption.NoCharge => "NoCharge", _ => "Billable" } });
        if (ops.Count == 0) return new UpdateTimeEntryResult(true, null);

        await SendAsync<CwRef>(HttpMethod.Patch, $"time/entries/{entryId}", ops, ct);
        return new UpdateTimeEntryResult(true, null);
    }

    public async Task<UpdateTimeEntryResult> DeleteTimeEntryAsync(string entryId, CancellationToken ct = default)
    {
        await SendVoidAsync(HttpMethod.Delete, $"time/entries/{entryId}", null, ct);
        return new UpdateTimeEntryResult(true, null);
    }

    public async Task<IReadOnlyList<ExternalFieldOption>> GetStatusesAsync(CancellationToken ct = default)
    {
        var board = (await GetListAsync<CwRef>("service/boards", new() { ["pageSize"] = "1" }, ct)).FirstOrDefault();
        if (board is null) return [];
        var statuses = await GetListAsync<CwRef>($"service/boards/{board.Id}/statuses", new() { ["pageSize"] = "1000" }, ct);
        return statuses.Select(s => new ExternalFieldOption(s.Id.ToString(), s.Name ?? "")).ToList();
    }

    public async Task<IReadOnlyList<ExternalFieldOption>> GetPrioritiesAsync(CancellationToken ct = default)
    {
        var items = await GetListAsync<CwRef>("service/priorities", new() { ["pageSize"] = "1000" }, ct);
        return items.Select(p => new ExternalFieldOption(p.Id.ToString(), p.Name ?? "")).ToList();
    }

    public async Task<IReadOnlyList<ExternalFieldOption>> GetQueuesOrBoardsAsync(CancellationToken ct = default)
    {
        var items = await GetListAsync<CwRef>("service/boards", new() { ["pageSize"] = "1000" }, ct);
        return items.Select(b => new ExternalFieldOption(b.Id.ToString(), b.Name ?? "")).ToList();
    }

    public async Task<IReadOnlyList<ExternalFieldOption>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var board = (await GetListAsync<CwRef>("service/boards", new() { ["pageSize"] = "1" }, ct)).FirstOrDefault();
        if (board is null) return [];
        var types = await GetListAsync<CwRef>($"service/boards/{board.Id}/types", new() { ["pageSize"] = "1000" }, ct);
        return types.Select(t => new ExternalFieldOption(t.Id.ToString(), t.Name ?? "")).ToList();
    }

    public async Task<IReadOnlyList<ExternalFieldOption>> GetWorkTypesAsync(CancellationToken ct = default)
    {
        var items = await GetListAsync<CwRef>("time/workTypes", new() { ["pageSize"] = "1000" }, ct);
        return items.Select(w => new ExternalFieldOption(w.Id.ToString(), w.Name ?? "")).ToList();
    }

    public async Task<IReadOnlyList<ExternalFieldOption>> GetWorkRolesAsync(CancellationToken ct = default)
    {
        var items = await GetListAsync<CwRef>("time/workRoles", new() { ["pageSize"] = "1000" }, ct);
        return items.Select(w => new ExternalFieldOption(w.Id.ToString(), w.Name ?? "")).ToList();
    }

    public Task<IReadOnlyList<ExternalFieldDefinition>> GetCustomFieldsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExternalFieldDefinition>>([]);

    public Task<WebhookValidationResult> ValidateWebhookAsync(WebhookRequest request, CancellationToken ct = default)
    {
        if (!request.Headers.TryGetValue("X-Timestamp", out var tsRaw) || !DateTimeOffset.TryParse(tsRaw, out var ts))
            return Task.FromResult(new WebhookValidationResult(false, "Missing or invalid timestamp."));
        if (Math.Abs((request.ReceivedAt - ts).TotalSeconds) > config.WebhookMaxSkew.TotalSeconds)
            return Task.FromResult(new WebhookValidationResult(false, "Timestamp outside allowed skew."));

        var expected = Hmac(request.Body, config.WebhookSecret);
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(request.RawSignature ?? ""));
        return Task.FromResult(ok
            ? new WebhookValidationResult(true, null)
            : new WebhookValidationResult(false, "Signature mismatch."));
    }

    public Task<NormalizedProviderEvent> ProcessWebhookAsync(WebhookRequest request, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(request.Body);
        var root = doc.RootElement;
        var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "unknown" : "unknown";
        var ticketId = root.TryGetProperty("ticketId", out var ti) ? ti.GetString() : null;
        var key = root.TryGetProperty("id", out var idp) ? idp.GetString() ?? Hmac(request.Body, "idem") : Hmac(request.Body, "idem");
        return Task.FromResult(new NormalizedProviderEvent(eventType, ticketId, key, request.ReceivedAt));
    }

    // ---- HTTP plumbing ----

    /// <summary>A CW reference: numeric value → {id}, otherwise {name}. Mapping supplies ids in production.</summary>
    private static object Ref(string value) =>
        long.TryParse(value, out var id) ? new { id } : new { name = value };

    private static readonly string[] ClosedFamily = ["closed", "resolved", "completed", "done", "finished"];

    /// <summary>CW has no IN operator, so an id list becomes an OR group: (f=1 or f=2).</summary>
    private static string IdGroup(string field, IReadOnlyList<string> ids)
        => "(" + string.Join(" or ", ids.Select(i => $"{field}={i}")) + ")";

    /// <summary>
    /// Statuses in ConnectWise are BOARD-scoped: each service board defines its own set, so a status
    /// name (or an id from another board) is invalid on this ticket's board. Resolve the desired
    /// value against the ticket's own board with normalized matching (exact → prefix → contains) so
    /// portal-neutral values like IN_PROGRESS find "In Progress (plan of action)". Fails with the
    /// board's actual options so the caller can correct the mapping.
    /// </summary>
    private async Task<object> ResolveBoardStatusAsync(long? boardId, string desired, CancellationToken ct)
    {
        if (boardId is null) return Ref(desired); // no board on the ticket — let CW validate

        var statuses = await GetListAsync<CwRef>($"service/boards/{boardId}/statuses", new() { ["pageSize"] = "1000" }, ct);
        if (long.TryParse(desired, out var id))
        {
            if (statuses.Any(s => s.Id == id)) return new { id };
            // An id from a different board — fall through to name matching below is pointless; report clearly.
            throw new ConnectorException(ConnectorFailureKind.InvalidRequest,
                $"Status id {id} does not exist on this ticket's board. Available: {string.Join(", ", statuses.Select(s => s.Name))}.");
        }

        static string Norm(string s) => new([.. s.ToLowerInvariant().Where(char.IsLetterOrDigit)]);
        var want = Norm(desired);
        var match = statuses.FirstOrDefault(s => Norm(s.Name ?? "") == want)
                 ?? statuses.FirstOrDefault(s => Norm(s.Name ?? "").StartsWith(want))
                 ?? statuses.FirstOrDefault(s => Norm(s.Name ?? "").Contains(want));

        // Boards name their terminal state differently ("Completed", "Closed (resolved)", "Done").
        // For closed-family requests, fall back to closed-family synonyms before giving up.
        if (match is null && ClosedFamily.Contains(want))
            match = statuses.FirstOrDefault(s => ClosedFamily.Any(syn => Norm(s.Name ?? "").StartsWith(syn)));

        if (match is null)
            throw new ConnectorException(ConnectorFailureKind.InvalidRequest,
                $"No status matching '{desired}' on this ticket's board. Available: {string.Join(", ", statuses.Select(s => s.Name))}.");
        return new { id = match.Id };
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private async Task<List<T>> GetListAsync<T>(string path, Dictionary<string, string> query, CancellationToken ct)
        => await SendAsync<List<T>>(HttpMethod.Get, BuildPath(path, query), null, ct) ?? [];

    private async Task<T?> GetOneAsync<T>(string path, CancellationToken ct) where T : class
    {
        try { return await SendAsync<T>(HttpMethod.Get, path, null, ct); }
        catch (ConnectorException ex) when (ex.Kind == ConnectorFailureKind.NotFound) { return null; }
    }

    private static string BuildPath(string path, Dictionary<string, string> query)
    {
        if (query.Count == 0) return path;
        var qs = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return $"{path}?{qs}";
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{config.Credentials.CompanyId}+{config.Credentials.PublicKey}:{config.Credentials.PrivateKey}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.Add("clientId", config.Credentials.ClientId);
        if (body is not null)
            req.Content = JsonContent.Create(body, options: JsonOpts);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ConnectorException(ConnectorFailureKind.Timeout, "ConnectWise request timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new ConnectorException(ConnectorFailureKind.Timeout, "ConnectWise request failed.", ex);
        }

        if (!resp.IsSuccessStatusCode)
            throw MapError(resp);

        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
    }

    /// <summary>Send a request that returns no body (e.g. DELETE → 204). Same auth/error handling.</summary>
    private async Task SendVoidAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{config.Credentials.CompanyId}+{config.Credentials.PublicKey}:{config.Credentials.PrivateKey}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.Add("clientId", config.Credentials.ClientId);
        if (body is not null)
            req.Content = JsonContent.Create(body, options: JsonOpts);

        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req, ct); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { throw new ConnectorException(ConnectorFailureKind.Timeout, "ConnectWise request timed out."); }
        catch (HttpRequestException ex)
        { throw new ConnectorException(ConnectorFailureKind.Timeout, "ConnectWise request failed.", ex); }

        if (!resp.IsSuccessStatusCode)
            throw MapError(resp);
    }

    private static ConnectorException MapError(HttpResponseMessage resp) => resp.StatusCode switch
    {
        HttpStatusCode.Unauthorized => new(ConnectorFailureKind.Authentication, "ConnectWise rejected the credentials."),
        HttpStatusCode.Forbidden => new(ConnectorFailureKind.PermissionDenied, "ConnectWise denied permission."),
        HttpStatusCode.NotFound => new(ConnectorFailureKind.NotFound, "ConnectWise entity not found."),
        HttpStatusCode.TooManyRequests => new(ConnectorFailureKind.RateLimited, "ConnectWise rate limit hit.")
        {
            RetryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10),
        },
        >= HttpStatusCode.InternalServerError => new(ConnectorFailureKind.ProviderError, $"ConnectWise server error ({(int)resp.StatusCode})."),
        _ => new(ConnectorFailureKind.InvalidRequest, $"ConnectWise rejected the request ({(int)resp.StatusCode})."),
    };

    private UnifiedTicket ToUnified(CwTicket t) => new()
    {
        ExternalId = t.Id.ToString(),
        Title = t.Summary ?? "",
        Description = t.InitialDescription,
        Status = t.Status?.Name,
        Priority = t.Priority?.Name,
        Category = t.Type?.Name,
        QueueOrBoard = t.Board?.Name,          // Service Board → portal Queue
        AssignedTechnicianExternalId = t.Owner?.Id.ToString(),
        RequesterExternalId = t.Company?.Id.ToString(),
        CompanyName = t.Company?.Name,
        ModifiedAt = t.LastUpdated,
        ResolvedAt = t.DateResolved,
    };

    private static string Hmac(string body, string secret)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
}
