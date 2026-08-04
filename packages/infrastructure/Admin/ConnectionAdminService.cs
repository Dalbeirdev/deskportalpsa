using Desk.Application.Abstractions;
using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Persistence;
using Desk.PsaCore.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Admin;

public sealed class ConnectionAdminService(
    DeskDbContext db,
    ISecretStore secrets,
    IAuditWriter audit,
    IConnectorResolver connectors,
    IConnectionFieldCache fieldCache,
    TimeProvider clock) : IConnectionAdminService
{
    public async Task<IReadOnlyList<ConnectionSummary>> ListAsync(CancellationToken ct = default)
        => await db.PsaConnections.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ConnectionSummary(
                c.Id, c.Name, c.Provider, c.ApiEndpoint, c.TenantIdentifier,
                c.Status, c.IsEnabled, c.LastSuccessfulSyncAt, c.LastError))
            // CredentialSecretRef is intentionally never projected.
            .ToListAsync(ct);

    public async Task<ConnectionSummary> CreateAsync(CreateConnectionInput input, CancellationToken ct = default)
    {
        if (input.Credentials.Count == 0)
            throw new ValidationFailedException("At least one credential value is required.");

        // Secret goes to Vault; only the opaque reference is persisted on the row.
        var secretRef = await secrets.WriteAsync($"{input.Provider}/{input.Name}", input.Credentials, ct);

        var connection = new PsaConnection
        {
            Name = input.Name,
            Provider = input.Provider,
            ApiEndpoint = input.ApiEndpoint,
            TenantIdentifier = input.TenantIdentifier,
            CredentialSecretRef = secretRef,
            TimeZone = input.TimeZone ?? "UTC",
            Status = ConnectionStatus.Pending,
            IsEnabled = true,
        };
        db.PsaConnections.Add(connection);
        await db.SaveChangesAsync(ct);

        // Audit detail deliberately excludes credentials.
        await audit.WriteAsync("connection.created", "PsaConnection", connection.Id.ToString(),
            new { connection.Name, connection.Provider, connection.ApiEndpoint }, ct);

        // Fetch field options once at configure time (best-effort — creds may not be valid yet).
        await TryCacheFieldsAsync(connection.Id, ct);

        return new ConnectionSummary(connection.Id, connection.Name, connection.Provider, connection.ApiEndpoint,
            connection.TenantIdentifier, connection.Status, connection.IsEnabled, null, null);
    }

    public async Task SetEnabledAsync(Guid connectionId, bool enabled, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
            ?? throw new NotFoundException("PSA connection");
        connection.IsEnabled = enabled;
        if (!enabled) connection.Status = ConnectionStatus.Disabled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(enabled ? "connection.enabled" : "connection.disabled",
            "PsaConnection", connectionId.ToString(), null, ct);
    }

    public async Task<ConnectionTestResultDto> TestAsync(Guid connectionId, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        ConnectionTestResultDto dto;
        try
        {
            var connector = await connectors.ResolveAsync(connectionId, ct);
            var result = await connector.TestConnectionAsync(ct);
            connection.Status = result.Success ? ConnectionStatus.Healthy : ConnectionStatus.Failed;
            connection.LastError = result.Success ? null : result.Message;
            dto = new ConnectionTestResultDto(result.Success, result.Message, result.Latency.TotalMilliseconds);
        }
        catch (ConnectorException ex)
        {
            connection.Status = ConnectionStatus.Failed;
            connection.LastError = ex.Message;
            dto = new ConnectionTestResultDto(false, $"{ex.Kind}: {ex.Message}", 0);
        }

        connection.LastHealthCheckAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("connection.tested", "PsaConnection", connectionId.ToString(),
            new { dto.Success, dto.Message }, ct);

        // A successful test means creds work — refresh the field cache off the same configure action.
        if (dto.Success) await TryCacheFieldsAsync(connectionId, ct);
        return dto;
    }

    public async Task<ConnectionSummary> UpdateAsync(Guid connectionId, UpdateConnectionInput input, CancellationToken ct = default)
    {
        var connection = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        connection.Name = input.Name;
        connection.ApiEndpoint = input.ApiEndpoint;
        connection.TenantIdentifier = input.TenantIdentifier;
        connection.TimeZone = input.TimeZone ?? connection.TimeZone;
        connection.IsEnabled = input.IsEnabled;
        if (!input.IsEnabled) connection.Status = ConnectionStatus.Disabled;

        // Rotate credentials only if new ones were supplied — the secret ref stays the same.
        var rotated = input.Credentials is { Count: > 0 };
        if (rotated)
            await secrets.RotateAsync(connection.CredentialSecretRef, input.Credentials!, ct);

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("connection.updated", "PsaConnection", connectionId.ToString(),
            new { connection.Name, connection.ApiEndpoint, CredentialsRotated = rotated }, ct);

        // Endpoint/creds may have changed — drop the cache so the next read re-discovers.
        fieldCache.Remove(connectionId);

        return new ConnectionSummary(connection.Id, connection.Name, connection.Provider, connection.ApiEndpoint,
            connection.TenantIdentifier, connection.Status, connection.IsEnabled, connection.LastSuccessfulSyncAt, connection.LastError);
    }

    /// <summary>Returns cached field options if present (populated at configure time), else discovers
    /// live once and caches. Use <see cref="RefreshFieldsAsync"/> to force a fresh pull.</summary>
    public async Task<ConnectionFieldsDto> GetFieldsAsync(Guid connectionId, CancellationToken ct = default)
    {
        _ = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        if (fieldCache.Get(connectionId) is { } cached) return cached;
        var fields = await DiscoverAsync(connectionId, ct);
        fieldCache.Set(connectionId, fields);
        return fields;
    }

    public async Task<ConnectionSettingsDto> GetSettingsAsync(Guid connectionId, CancellationToken ct = default)
    {
        var c = await db.PsaConnections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == connectionId, ct)
            ?? throw new NotFoundException("PSA connection");
        return ToSettings(c);
    }

    public async Task<ConnectionSettingsDto> SaveSettingsAsync(Guid connectionId, ConnectionSettingsDto input, CancellationToken ct = default)
    {
        var c = await db.PsaConnections.FirstOrDefaultAsync(x => x.Id == connectionId, ct)
            ?? throw new NotFoundException("PSA connection");

        if (!input.ImportOpenTickets && !input.ImportClosedTickets)
            throw new ValidationFailedException("Select at least one of open or closed tickets to import.");
        if (input.FilterActiveWithinDays is < 0)
            throw new ValidationFailedException("Active-within days cannot be negative.");

        c.TwoWaySync = input.TwoWaySync;
        c.AutoImportNewTickets = input.AutoImportNewTickets;
        // Note import only makes sense when provider changes flow back at all.
        c.ImportNotes = input.ImportNotes && input.TwoWaySync;
        c.ImportSystemNotes = input.ImportSystemNotes && c.ImportNotes;
        c.SyncAttachments = input.SyncAttachments;
        c.ImportOpenTickets = input.ImportOpenTickets;
        c.ImportClosedTickets = input.ImportClosedTickets;
        c.FilterCompanyIds = Clean(input.FilterCompanyIds);
        c.FilterQueueIds = Clean(input.FilterQueueIds);
        c.FilterResourceIds = Clean(input.FilterResourceIds);
        c.FilterActiveWithinDays = input.FilterActiveWithinDays is > 0 ? input.FilterActiveWithinDays : null;
        c.DefaultQueueOrBoardId = Clean(input.DefaultQueueOrBoardId);
        c.DefaultTicketType = Clean(input.DefaultTicketType);
        c.DefaultIssueType = Clean(input.DefaultIssueType);
        c.DefaultSubIssueType = Clean(input.DefaultSubIssueType);

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("connection.settings.updated", "PsaConnection", connectionId.ToString(),
            new { c.TwoWaySync, c.AutoImportNewTickets, c.ImportOpenTickets, c.ImportClosedTickets }, ct);
        return ToSettings(c);
    }

    private static ConnectionSettingsDto ToSettings(Desk.Domain.Tenancy.PsaConnection c) => new(
        c.TwoWaySync, c.AutoImportNewTickets, c.ImportNotes, c.ImportSystemNotes, c.SyncAttachments,
        c.ImportOpenTickets, c.ImportClosedTickets,
        c.FilterCompanyIds, c.FilterQueueIds, c.FilterResourceIds, c.FilterActiveWithinDays,
        c.DefaultQueueOrBoardId, c.DefaultTicketType, c.DefaultIssueType, c.DefaultSubIssueType);

    /// <summary>Normalizes a comma-separated id list; empty becomes null (= no restriction).</summary>
    private static string? Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : string.Join(",", parts);
    }

    public async Task<ConnectionFieldsDto> RefreshFieldsAsync(Guid connectionId, CancellationToken ct = default)
    {
        _ = await db.PsaConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
            ?? throw new NotFoundException("PSA connection");
        var fields = await DiscoverAsync(connectionId, ct);
        fieldCache.Set(connectionId, fields);
        await audit.WriteAsync("connection.fields.refreshed", "PsaConnection", connectionId.ToString(), null, ct);
        return fields;
    }

    // Live discovery from the connected PSA. Individual lookups degrade to empty rather than
    // failing the whole request if the provider doesn't support one.
    private async Task<ConnectionFieldsDto> DiscoverAsync(Guid connectionId, CancellationToken ct)
    {
        var connector = await connectors.ResolveAsync(connectionId, ct);
        var boards = await SafeAsync(() => connector.GetQueuesOrBoardsAsync(ct));
        var statuses = await SafeAsync(() => connector.GetStatusesAsync(ct));
        var priorities = await SafeAsync(() => connector.GetPrioritiesAsync(ct));
        var categories = await SafeAsync(() => connector.GetCategoriesAsync(ct));
        var workTypes = await SafeAsync(() => connector.GetWorkTypesAsync(ct));
        var workRoles = await SafeAsync(() => connector.GetWorkRolesAsync(ct));
        return new ConnectionFieldsDto(Map(boards), Map(statuses), Map(priorities), Map(categories), Map(workTypes), Map(workRoles));
    }

    private async Task TryCacheFieldsAsync(Guid connectionId, CancellationToken ct)
    {
        try { fieldCache.Set(connectionId, await DiscoverAsync(connectionId, ct)); }
        catch { /* creds may be invalid/unreachable at configure time — refresh later */ }
    }

    private static IReadOnlyList<FieldOptionDto> Map(IReadOnlyList<Desk.PsaCore.Models.ExternalFieldOption> options)
        => options.Select(o => new FieldOptionDto(o.Value, o.Label)).ToList();

    private static async Task<IReadOnlyList<Desk.PsaCore.Models.ExternalFieldOption>> SafeAsync(
        Func<Task<IReadOnlyList<Desk.PsaCore.Models.ExternalFieldOption>>> fetch)
    {
        try { return await fetch(); }
        catch (ConnectorException) { return []; }
    }
}
