using Desk.Application.Attachments;
using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Application.Mapping;
using Desk.Application.Sync;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Attachments;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Sync;
using Desk.PsaCore.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// A connection whose connector can never even be built — e.g. a secret-store outage lost its
/// credentials — must still be recorded as failed. Before this fix, ConnectorResolver.ResolveAsync
/// ran ahead of ConnectionSyncRunner's try/catch, so that specific failure escaped without ever
/// touching Status or LastError: the dashboard kept showing stale "Healthy" indefinitely while the
/// scheduled sync silently failed underneath it.
/// </summary>
public class ConnectionSyncRunnerFailureTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    private sealed class ThrowingResolver(Exception ex) : IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => throw ex;
    }

    private static async Task<DeskDbContext> SeedAsync(string dbName)
    {
        var db = TestDbContextFactory.ForPlatform(dbName);
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "CW", Provider = ProviderType.ConnectWisePsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "gone", Status = ConnectionStatus.Healthy,
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Connector_resolution_failure_marks_the_connection_degraded_with_a_reason()
    {
        var clock = new TestClock();
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var thrown = new ValidationFailedException("'CW' has no valid stored credentials — edit the connection and re-enter them.");
        var runner = new ConnectionSyncRunner(db, new ThrowingResolver(thrown),
            new TicketSyncService(db, new MappingEngine(), new SyncEventStore(db, clock), clock),
            new InMemoryObjectStorage(new AttachmentStorageOptions(), clock), new HeuristicMalwareScanner(), clock);

        var act = () => runner.RunAsync(Conn);

        await act.Should().ThrowAsync<ValidationFailedException>(); // caller (the worker) still sees the real failure
        var row = await db.PsaConnections.AsNoTracking().SingleAsync(c => c.Id == Conn);
        row.Status.Should().Be(ConnectionStatus.Degraded);
        row.LastError.Should().Contain("re-enter them");
    }
}
