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
        return items.Select(n => new UnifiedTicketNote(
            n.Id.ToString(), "Autotask", n.Description ?? "", IsPublic: true, n.CreateDateTime ?? clock.GetUtcNow())).ToList();
    }

    public async Task<CreateNoteResult> AddPublicNoteAsync(string ticketId, UnifiedTicketNoteCreateRequest note, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["ticketID"] = long.Parse(ticketId),
            ["description"] = note.Body,
            ["noteType"] = 1,
            ["publish"] = note.IsPublic ? config.PublicPublishValue : config.InternalPublishValue,
        };
        var result = await SendAsync<AtCreateResult>(HttpMethod.Post, "V1.0/TicketNotes", body, ct);
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
            throw MapError(resp);

        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    private static ConnectorException MapError(HttpResponseMessage resp) => resp.StatusCode switch
    {
        HttpStatusCode.Unauthorized => new(ConnectorFailureKind.Authentication, "Autotask rejected the credentials."),
        HttpStatusCode.Forbidden => new(ConnectorFailureKind.PermissionDenied, "Autotask denied permission."),
        HttpStatusCode.NotFound => new(ConnectorFailureKind.NotFound, "Autotask entity not found."),
        HttpStatusCode.TooManyRequests => new(ConnectorFailureKind.RateLimited, "Autotask rate limit hit.")
        {
            RetryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10),
        },
        >= HttpStatusCode.InternalServerError => new(ConnectorFailureKind.ProviderError, $"Autotask server error ({(int)resp.StatusCode})."),
        _ => new(ConnectorFailureKind.InvalidRequest, $"Autotask rejected the request ({(int)resp.StatusCode})."),
    };

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
