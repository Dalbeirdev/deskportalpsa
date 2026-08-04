using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desk.Domain.Enums;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;

namespace Desk.Connectors.Autotask;

/// <summary>
/// Datto Autotask PSA connector over the REST API v1.0. Bound to one connection; credentials are
/// injected by the factory (resolved from Vault). Provider-specific request/response handling and
/// HTTP→<see cref="ConnectorException"/> mapping live entirely here — callers stay provider-neutral.
///
/// Provider notes: Autotask statuses/priorities are numeric picklist ids (translated by the
/// platform mapping engine, not this connector); notes use a numeric <c>publish</c> flag, so
/// "public" is <see cref="AutotaskConnectorConfig.PublicPublishValue"/>.
/// </summary>
public sealed class AutotaskConnector(HttpClient http, AutotaskConnectorConfig config, TimeProvider clock)
    : IServiceManagementConnector
{
    // Autotask expects PascalCase request wrappers (MaxRecords/Filter). JsonContent.Create defaults
    // to camelCase (web defaults), so use explicit general options to preserve the names as written.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.General);

    public ProviderType Provider => ProviderType.AutotaskPsa;

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct = default) =>
        Task.FromResult(new ProviderCapabilities
        {
            SupportsTicketCreate = true, SupportsTicketUpdate = true, SupportsTicketDelete = false,
            SupportsPublicNotes = true, SupportsPrivateNotes = true, SupportsAttachments = true,
            SupportsTimeEntries = true, SupportsAssets = true, SupportsContracts = true,
            SupportsSlaData = true, SupportsCustomFields = true, SupportsInboundWebhooks = true,
            SupportsOutboundWebhooks = false, SupportsIncrementalSync = true, SupportsBulkRead = true,
            SupportsBulkWrite = false, SupportsCompanies = true, SupportsContacts = true,
            SupportsTechnicians = true, SupportsTeams = true, SupportsQueues = true,
            MaximumPageSize = 500, MaximumAttachmentSize = 6 * 1024 * 1024,
            RateLimitModel = "threshold-per-hour",
            AuthenticationTypes = [AuthenticationType.ApiKey],
        });

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var start = clock.GetTimestamp();
        await QueryAsync<AtCompany>("Companies", [Filter("id", "gte", 0)], 1, ct);
        return new ConnectionTestResult(true, "OK", clock.GetElapsedTime(start));
    }

    public async Task<IReadOnlyList<ExternalOrganization>> GetOrganizationsAsync(CancellationToken ct = default)
    {
        var items = await QueryAsync<AtCompany>("Companies", [Filter("id", "gte", 0)], 500, ct);
        return items.Select(c => new ExternalOrganization(c.Id.ToString(), c.CompanyName ?? "", c.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<ExternalContact>> GetContactsAsync(string organizationId, CancellationToken ct = default)
    {
        var items = await QueryAsync<AtContact>("Contacts", [Filter("companyID", "eq", long.Parse(organizationId))], 500, ct);
        return items.Select(c => new ExternalContact(
            c.Id.ToString(), c.EmailAddress ?? "", $"{c.FirstName} {c.LastName}".Trim(), c.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<ExternalTechnician>> GetTechniciansAsync(CancellationToken ct = default)
    {
        var items = await QueryAsync<AtResource>("Resources", [Filter("id", "gte", 0)], 500, ct);
        return items.Select(r => new ExternalTechnician(
            r.Id.ToString(), r.Email ?? "", $"{r.FirstName} {r.LastName}".Trim(), r.IsActive)).ToList();
    }

    public async Task<PaginatedResult<UnifiedTicket>> GetTicketsAsync(TicketFilter filter, CancellationToken ct = default)
    {
        var filters = new List<object>();
        if (filter.ModifiedSince is { } since)
            filters.Add(Filter("lastActivityDate", "gte", since.ToUniversalTime().ToString("o")));
        else
            filters.Add(Filter("id", "gte", 0));
        if (filter.ExternalCompanyId is { } company)
            filters.Add(Filter("companyID", "eq", long.Parse(company)));

        // Import filters. Autotask expresses "in" with the "in" operator over a value list.
        if (filter.CompanyIds.Count > 0)
            filters.Add(Filter("companyID", "in", filter.CompanyIds.Select(long.Parse).ToArray()));
        if (filter.QueueOrBoardIds.Count > 0)
            filters.Add(Filter("queueID", "in", filter.QueueOrBoardIds.Select(long.Parse).ToArray()));
        if (filter.AssignedResourceIds.Count > 0)
            filters.Add(Filter("assignedResourceID", "in", filter.AssignedResourceIds.Select(long.Parse).ToArray()));
        if (filter.ActiveWithinDays is > 0 and { } days)
            filters.Add(Filter("lastActivityDate", "gte", clock.GetUtcNow().AddDays(-days).ToString("o")));

        var items = await QueryAsync<AtTicket>("Tickets", filters, filter.PageSize, ct);
        return new PaginatedResult<UnifiedTicket>(items.Select(ToUnified).ToList(), null, false);
    }

    public async Task<UnifiedTicket?> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        var item = await GetByIdAsync<AtTicket>("Tickets", ticketId, ct);
        return item is null ? null : ToUnified(item);
    }

    public async Task<CreateTicketResult> CreateTicketAsync(UnifiedTicketCreateRequest ticket, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = ticket.Title,
            ["description"] = ticket.Description,
            ["status"] = ticket.Status,
            ["priority"] = ticket.Priority,
            ["queueID"] = ticket.QueueOrBoard,
            ["ticketCategory"] = ticket.Category,
            ["companyID"] = long.Parse(ticket.ExternalCompanyId),
        };
        // Optional classification — Autotask tenants differ on which of these are mandatory, so
        // only send what the connection actually configured.
        if (ticket.TicketType is not null) body["ticketType"] = ticket.TicketType;
        if (ticket.IssueType is not null) body["issueType"] = ticket.IssueType;
        if (ticket.SubIssueType is not null) body["subIssueType"] = ticket.SubIssueType;
        var result = await SendAsync<AtCreateResult>(HttpMethod.Post, "V1.0/Tickets", body, ct);
        return new CreateTicketResult(true, result!.ItemId.ToString(), null);
    }

    public async Task<UpdateTicketResult> UpdateTicketAsync(string ticketId, UnifiedTicketUpdate update, CancellationToken ct = default)
    {
        // Confirm existence so a missing ticket surfaces as NotFound rather than a silent no-op.
        _ = await GetByIdAsync<AtTicket>("Tickets", ticketId, ct)
            ?? throw new ConnectorException(ConnectorFailureKind.NotFound, $"Ticket {ticketId} not found.");

        var body = new Dictionary<string, object?> { ["id"] = long.Parse(ticketId) };
        if (update.Status is not null) body["status"] = update.Status;
        if (update.Priority is not null) body["priority"] = update.Priority;
        if (update.Category is not null) body["ticketCategory"] = update.Category;
        if (update.QueueOrBoard is not null) body["queueID"] = update.QueueOrBoard;
        if (update.AssignedTechnicianExternalId is not null) body["assignedResourceID"] = update.AssignedTechnicianExternalId;

        await SendAsync<AtCreateResult>(HttpMethod.Patch, "V1.0/Tickets", body, ct);
        return new UpdateTicketResult(true, null);
    }

    public async Task<IReadOnlyList<UnifiedTicketNote>> GetPublicNotesAsync(string ticketId, CancellationToken ct = default)
    {
        var items = await QueryAsync<AtTicketNote>("TicketNotes",
            [Filter("ticketID", "eq", long.Parse(ticketId)), Filter("publish", "eq", config.PublicPublishValue)], 500, ct);

        // Resolve the real author. Autotask puts only an id on the note — a resource (technician) or,
        // when a customer contact wrote it, a contact — so translate both to display names. A thread
        // that attributes every reply to "Autotask" is useless to a reader. Each lookup is a single
        // extra request, made only when a note in this batch actually needs it.
        var names = new Dictionary<long, string>();
        if (items.Any(n => n.CreatorResourceId is > 0))
            await SafeFillAsync(names, async () =>
                (await GetTechniciansAsync(ct)).Select(r => (r.ExternalId, r.DisplayName)));

        var contactNames = new Dictionary<long, string>();
        var contactIds = items.Where(n => n.CreatedByContactId is > 0).Select(n => n.CreatedByContactId!.Value).Distinct().ToList();
        if (contactIds.Count > 0)
            await SafeFillAsync(contactNames, async () =>
                (await QueryAsync<AtContact>("Contacts", [Filter("id", "in", contactIds.ToArray())], 500, ct))
                    .Select(c => (c.Id.ToString(), $"{c.FirstName} {c.LastName}".Trim())));

        return items.Select(n => new UnifiedTicketNote(
            n.Id.ToString(),
            AuthorOf(n, names, contactNames),
            n.Description ?? "", IsPublic: true, n.CreateDateTime ?? clock.GetUtcNow())).ToList();
    }

    private static string AuthorOf(AtTicketNote note, IReadOnlyDictionary<long, string> resources, IReadOnlyDictionary<long, string> contacts)
    {
        if (note.CreatedByContactId is { } cid && contacts.TryGetValue(cid, out var contact) && !string.IsNullOrWhiteSpace(contact))
            return contact;
        if (note.CreatorResourceId is { } rid && resources.TryGetValue(rid, out var resource) && !string.IsNullOrWhiteSpace(resource))
            return resource;
        // No resolvable author: a workflow rule, an SLA event, or a deleted resource. An empty author
        // is the contract's marker for a system-generated note — the sync layer filters on it.
        return "";
    }

    /// <summary>Best-effort name lookup: an author-name lookup must never fail the note read itself.</summary>
    private static async Task SafeFillAsync(Dictionary<long, string> target, Func<Task<IEnumerable<(string Id, string Name)>>> load)
    {
        try
        {
            foreach (var (id, name) in await load())
                if (long.TryParse(id, out var parsed)) target[parsed] = name;
        }
        catch (ConnectorException) { /* callers fall back to the neutral provider label */ }
    }

    public async Task<CreateNoteResult> AddPublicNoteAsync(string ticketId, UnifiedTicketNoteCreateRequest note, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["ticketID"] = long.Parse(ticketId),
            // Autotask requires a note title. The unified model has no separate title (portals and
            // most PSAs treat a reply as body-only), so derive a readable one from the first line
            // rather than inventing a tag — nothing is added to the body the customer sees.
            ["title"] = NoteTitle(note.Body),
            ["description"] = note.Body,
            ["noteType"] = 1,
            ["publish"] = note.IsPublic ? config.PublicPublishValue : config.InternalPublishValue,
        };
        // TicketNotes is a CHILD collection in Autotask: creates go to the parent ticket's /Notes
        // route. Posting to the top-level /V1.0/TicketNotes returns 404 ("entity not found").
        // Querying still uses the top-level TicketNotes entity (see GetPublicNotesAsync).
        var result = await SendAsync<AtCreateResult>(HttpMethod.Post, $"V1.0/Tickets/{long.Parse(ticketId)}/Notes", body, ct);
        return new CreateNoteResult(true, result!.ItemId.ToString(), null);
    }

    public Task<IReadOnlyList<UnifiedAttachment>> GetAttachmentsAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnifiedAttachment>>([]);

    public async Task<CreateAttachmentResult> AddAttachmentAsync(string ticketId, SecureAttachment attachment, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["parentID"] = long.Parse(ticketId),
            ["title"] = attachment.FileName,
            ["fullPath"] = attachment.StorageObjectKey,
            ["contentType"] = attachment.ContentType,
        };
        var result = await SendAsync<AtCreateResult>(HttpMethod.Post, "V1.0/TicketAttachments", body, ct);
        return new CreateAttachmentResult(true, result!.ItemId.ToString(), null);
    }

    // Autotask installed-products sync isn't wired yet; report none rather than failing the panel.
    public Task<IReadOnlyList<ExternalDevice>> GetDevicesAsync(string organizationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExternalDevice>>([]);

    public Task<IReadOnlyList<UnifiedTimeEntry>> GetTimeEntriesAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnifiedTimeEntry>>([]);

    // Autotask time-entry write is not wired yet (Phase 1 targets ConnectWise); fail clearly rather
    // than silently succeed so callers surface an honest message.
    public Task<CreateTimeEntryResult> AddTimeEntryAsync(string ticketId, UnifiedTimeEntryCreateRequest entry, CancellationToken ct = default)
        => Task.FromResult(new CreateTimeEntryResult(false, null, "Time logging is not yet supported for Autotask."));

    public Task<UpdateTimeEntryResult> UpdateTimeEntryAsync(string entryId, UnifiedTimeEntryUpdate update, CancellationToken ct = default)
        => Task.FromResult(new UpdateTimeEntryResult(false, "Time editing is not yet supported for Autotask."));

    public Task<UpdateTimeEntryResult> DeleteTimeEntryAsync(string entryId, CancellationToken ct = default)
        => Task.FromResult(new UpdateTimeEntryResult(false, "Time deletion is not yet supported for Autotask."));

    public Task<IReadOnlyList<ExternalFieldOption>> GetStatusesAsync(CancellationToken ct = default) => PicklistAsync("status", ct);
    public Task<IReadOnlyList<ExternalFieldOption>> GetPrioritiesAsync(CancellationToken ct = default) => PicklistAsync("priority", ct);
    public Task<IReadOnlyList<ExternalFieldOption>> GetQueuesOrBoardsAsync(CancellationToken ct = default) => PicklistAsync("queueID", ct);
    public Task<IReadOnlyList<ExternalFieldOption>> GetCategoriesAsync(CancellationToken ct = default) => PicklistAsync("ticketCategory", ct);
    public Task<IReadOnlyList<ExternalFieldOption>> GetWorkTypesAsync(CancellationToken ct = default) => PicklistAsync("allocationCodeID", ct);
    public Task<IReadOnlyList<ExternalFieldOption>> GetWorkRolesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ExternalFieldOption>>([]);

    public async Task<IReadOnlyList<ExternalFieldDefinition>> GetCustomFieldsAsync(CancellationToken ct = default)
    {
        var info = await SendAsync<AtFieldInfoResult>(HttpMethod.Get, "V1.0/Tickets/entityInformation/userDefinedFields", null, ct);
        return (info?.Fields ?? []).Select(f => new ExternalFieldDefinition(
            f.Name ?? "", f.Name ?? "", "string", false)).ToList();
    }

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

    private async Task<IReadOnlyList<ExternalFieldOption>> PicklistAsync(string fieldName, CancellationToken ct)
    {
        var info = await SendAsync<AtFieldInfoResult>(HttpMethod.Get, "V1.0/Tickets/entityInformation/fields", null, ct);
        var field = info?.Fields.FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        return (field?.PicklistValues ?? [])
            .Select(p => new ExternalFieldOption(p.Value ?? "", p.Label ?? p.Value ?? "", p.IsActive)).ToList();
    }

    /// <summary>Autotask note titles are mandatory and capped; use the first meaningful line.</summary>
    private static string NoteTitle(string body)
    {
        var line = (body ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Length > 0) ?? "Portal reply";
        return line.Length <= 250 ? line : line[..250];
    }

    private static object Filter(string field, string op, object value) => new { op, field, value };

    private async Task<List<T>> QueryAsync<T>(string entity, List<object> filters, int maxRecords, CancellationToken ct)
    {
        var body = new { MaxRecords = maxRecords, Filter = filters };
        var result = await SendAsync<AtQueryResult<T>>(HttpMethod.Post, $"V1.0/{entity}/query", body, ct);
        return result?.Items ?? [];
    }

    private async Task<T?> GetByIdAsync<T>(string entity, string id, CancellationToken ct) where T : class
    {
        try
        {
            var result = await SendAsync<AtItemResult<T>>(HttpMethod.Get, $"V1.0/{entity}/{id}", null, ct);
            return result?.Item;
        }
        catch (ConnectorException ex) when (ex.Kind == ConnectorFailureKind.NotFound)
        {
            return null;
        }
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path);
        req.Headers.Add("ApiIntegrationCode", config.Credentials.ApiIntegrationCode);
        req.Headers.Add("UserName", config.Credentials.UserName);
        req.Headers.Add("Secret", config.Credentials.Secret);
        if (body is not null)
            req.Content = JsonContent.Create(body, options: JsonOpts);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ConnectorException(ConnectorFailureKind.Timeout, "Autotask request timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new ConnectorException(ConnectorFailureKind.Timeout, "Autotask request failed.", ex);
        }

        if (!resp.IsSuccessStatusCode)
            throw MapError(resp, await SafeBodyAsync(resp, ct));

        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    /// <summary>
    /// Reads the response body (best effort) so provider validation messages reach the caller.
    /// Autotask explains WHY a create failed in an "errors" array — without it every failure reads
    /// as an opaque 500 and admins cannot tell which field is wrong.
    /// </summary>
    private static async Task<string?> SafeBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array)
                    return string.Join("; ", errs.EnumerateArray().Select(e => e.ToString()));
            }
            catch (JsonException) { /* not JSON — fall through to the raw text */ }
            return raw.Length > 400 ? raw[..400] : raw;
        }
        catch { return null; }
    }

    private static ConnectorException MapError(HttpResponseMessage resp, string? body)
    {
        var detail = string.IsNullOrWhiteSpace(body) ? "" : $" {body}";
        return resp.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new(ConnectorFailureKind.Authentication, "Autotask rejected the credentials."),
            HttpStatusCode.Forbidden => new(ConnectorFailureKind.PermissionDenied, "Autotask denied permission."),
            HttpStatusCode.NotFound => new(ConnectorFailureKind.NotFound, "Autotask entity not found."),
            HttpStatusCode.TooManyRequests => new(ConnectorFailureKind.RateLimited, "Autotask rate limit hit.")
            {
                RetryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10),
            },
            >= HttpStatusCode.InternalServerError => new(ConnectorFailureKind.ProviderError, $"Autotask server error ({(int)resp.StatusCode}).{detail}"),
            _ => new(ConnectorFailureKind.InvalidRequest, $"Autotask rejected the request ({(int)resp.StatusCode}).{detail}"),
        };
    }

    private UnifiedTicket ToUnified(AtTicket t) => new()
    {
        ExternalId = t.Id.ToString(),
        Title = t.Title ?? "",
        Description = t.Description,
        Status = t.Status,
        Priority = t.Priority,
        Category = t.Category,
        QueueOrBoard = t.QueueId,
        AssignedTechnicianExternalId = t.AssignedResourceId,
        RequesterExternalId = t.CompanyId.ToString(),
        CreatedAt = t.CreateDate,
        ModifiedAt = t.LastActivityDate,
        ResolvedAt = t.ResolvedDateTime,
    };

    private static string Hmac(string body, string secret)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
}
