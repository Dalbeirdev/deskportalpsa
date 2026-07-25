using Desk.Application.Abstractions;
using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Admin;

public sealed class ConnectionAdminService(
    DeskDbContext db,
    ISecretStore secrets,
    IAuditWriter audit) : IConnectionAdminService
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
}
