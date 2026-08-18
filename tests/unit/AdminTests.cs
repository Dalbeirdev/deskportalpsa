using Desk.Infrastructure.Attachments;
using Desk.Application.Attachments;
using Desk.Application.Admin;
using Desk.Application.Common;
using Desk.Application.Connectors;
using Desk.Connectors.Mock;
using Desk.Domain.Enums;
using Desk.Domain.Sync;
using Desk.Infrastructure.Admin;
using Desk.PsaCore.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

public class AdminTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    /// <summary>Simulates what the real ConnectorResolver throws when a connection's credentials
    /// cannot be resolved at all — e.g. a secret-store outage lost them.</summary>
    private sealed class ThrowingResolver(Exception ex) : IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => throw ex;
    }

    private static (ConnectionAdminService svc, AdminHarness h) Connections(IServiceManagementConnector? connector = null)
    {
        var h = AdminHarness.Create(Org);
        var resolver = new FakeResolver(connector ?? new MockConnector(new MockConnectorOptions(), h.Clock));
        return (ConnectionsSvc(h, resolver), h);
    }

    private static (ConnectionAdminService svc, AdminHarness h) ConnectionsWithResolver(IConnectorResolver resolver)
    {
        var h = AdminHarness.Create(Org);
        return (ConnectionsSvc(h, resolver), h);
    }

    private static ConnectionAdminService ConnectionsSvc(AdminHarness h, IConnectorResolver resolver)
    {
        var audit = new AuditWriter(h.Db, h.User, h.Tenant, h.Clock);
        return new ConnectionAdminService(h.Db, h.Secrets, audit, resolver, new ConnectionFieldCache(),
            new InMemoryObjectStorage(new AttachmentStorageOptions(), h.Clock), h.Clock);
    }

    private static async Task<Guid> CreateConnAsync(ConnectionAdminService svc)
        => (await svc.CreateAsync(new CreateConnectionInput(
            "CW", ProviderType.ConnectWisePsa, "https://x", null,
            new Dictionary<string, string> { ["CompanyId"] = "c", ["PrivateKey"] = "p" }, null))).Id;

    private static (MappingAdminService svc, AdminHarness h) Mappings()
    {
        var h = AdminHarness.Create(Org);
        var audit = new AuditWriter(h.Db, h.User, h.Tenant, h.Clock);
        return (new MappingAdminService(h.Db, audit, h.User), h);
    }

    [Fact]
    public async Task Creating_a_connection_stores_the_secret_in_vault_never_on_the_row()
    {
        var (svc, h) = Connections();
        var creds = new Dictionary<string, string> { ["CompanyId"] = "acme", ["PrivateKey"] = "topsecret" };

        var summary = await svc.CreateAsync(new CreateConnectionInput(
            "Prod CW", ProviderType.ConnectWisePsa, "https://cw.local", "tenant-1", creds, "UTC"));

        var row = await h.Db.PsaConnections.SingleAsync();
        row.CredentialSecretRef.Should().StartWith("mem://");   // opaque reference, not the secret
        row.CredentialSecretRef.Should().NotContain("topsecret");

        // The real secret is retrievable only via the store.
        (await h.Secrets.ReadAsync(row.CredentialSecretRef))["PrivateKey"].Should().Be("topsecret");

        // The summary DTO has no property that could carry a secret (compile-time guarantee).
        summary.Should().BeOfType<ConnectionSummary>();
    }

    [Fact]
    public async Task Connection_creation_is_audited_without_credentials()
    {
        var (svc, h) = Connections();
        await svc.CreateAsync(new CreateConnectionInput(
            "Prod CW", ProviderType.ConnectWisePsa, "https://cw.local", null,
            new Dictionary<string, string> { ["PrivateKey"] = "topsecret" }, null));

        var entry = await h.Db.AuditLog.SingleAsync(a => a.Action == "connection.created");
        entry.DetailJson.Should().NotBeNull();
        entry.DetailJson!.Should().NotContain("topsecret"); // secret never enters the audit trail
        entry.ActorDisplayName.Should().Be("Admin User");
    }

    [Fact]
    public async Task Test_connection_marks_healthy_on_success_and_audits()
    {
        var (svc, h) = Connections(); // default mock connector succeeds
        var id = await CreateConnAsync(svc);

        var result = await svc.TestAsync(id);

        result.Success.Should().BeTrue();
        (await h.Db.PsaConnections.FirstAsync(c => c.Id == id)).Status.Should().Be(ConnectionStatus.Healthy);
        (await h.Db.AuditLog.CountAsync(a => a.Action == "connection.tested")).Should().Be(1);
    }

    [Fact]
    public async Task Test_connection_marks_failed_on_auth_error()
    {
        var h0 = AdminHarness.Create(Org);
        var failing = new MockConnector(new MockConnectorOptions { FailEveryCallWith = ConnectorFailureKind.Authentication }, h0.Clock);
        var (svc, h) = Connections(failing);
        var id = await CreateConnAsync(svc);

        var result = await svc.TestAsync(id);

        result.Success.Should().BeFalse();
        var row = await h.Db.PsaConnections.FirstAsync(c => c.Id == id);
        row.Status.Should().Be(ConnectionStatus.Failed);
        row.LastError.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_connection_marks_failed_when_the_connector_cannot_even_be_resolved()
    {
        // Simulates a connection whose credentials the secret store lost (a resolver that throws
        // before a connector is ever built) — this must still record Failed + a reason on the row,
        // not silently leave Status/LastError untouched, and must not surface as an unhandled 500.
        var thrown = new ValidationFailedException("'CW' has no valid stored credentials — edit the connection and re-enter them.");
        var (svc, h) = ConnectionsWithResolver(new ThrowingResolver(thrown));
        var id = await CreateConnAsync(svc);

        var result = await svc.TestAsync(id);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("re-enter them");
        var row = await h.Db.PsaConnections.FirstAsync(c => c.Id == id);
        row.Status.Should().Be(ConnectionStatus.Failed);
        row.LastError.Should().Contain("re-enter them");
    }

    [Fact]
    public async Task Update_changes_settings_and_audits()
    {
        var (svc, h) = Connections();
        var id = await CreateConnAsync(svc);

        await svc.UpdateAsync(id, new UpdateConnectionInput("Renamed", "https://new", "t2", "UTC", true, null));

        var row = await h.Db.PsaConnections.FirstAsync(c => c.Id == id);
        row.Name.Should().Be("Renamed");
        row.ApiEndpoint.Should().Be("https://new");
        (await h.Db.AuditLog.CountAsync(a => a.Action == "connection.updated")).Should().Be(1);
    }

    [Fact]
    public async Task Get_fields_discovers_boards_statuses_and_priorities()
    {
        var (svc, h) = Connections();
        var id = await CreateConnAsync(svc);

        var fields = await svc.GetFieldsAsync(id);

        fields.QueuesOrBoards.Should().NotBeEmpty();
        fields.Statuses.Should().NotBeEmpty();
        fields.Priorities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Mapping_upsert_creates_a_version_snapshot_and_audit_entry()
    {
        var (svc, h) = Mappings();
        await svc.UpsertAsync(new UpsertMappingInput(
            null, ProviderType.AutotaskPsa, MappingScope.ProviderDefault, null,
            "status", "IN_PROGRESS", "status", "In Progress", MappingDirection.Bidirectional, false, null), "initial");

        (await h.Db.FieldMappings.CountAsync()).Should().Be(1);
        (await h.Db.FieldMappingVersions.CountAsync()).Should().Be(1);
        (await h.Db.AuditLog.CountAsync(a => a.Action == "mapping.upserted")).Should().Be(1);
    }

    [Fact]
    public async Task Mapping_upsert_without_id_updates_the_matching_rule_instead_of_duplicating()
    {
        var (svc, h) = Mappings();
        UpsertMappingInput Input(string external) => new(
            null, ProviderType.AutotaskPsa, MappingScope.ProviderDefault, null,
            "status", "IN_PROGRESS", "status", external, MappingDirection.Bidirectional, false, null);

        await svc.UpsertAsync(Input("In Progress"), "first");
        await svc.UpsertAsync(Input("Working"), "repeat");

        // Same provider/scope/field/portal-value/direction → one rule, updated in place.
        (await h.Db.FieldMappings.CountAsync()).Should().Be(1);
        (await h.Db.FieldMappings.Select(m => m.ExternalValue).SingleAsync()).Should().Be("Working");
    }

    [Fact]
    public async Task Mapping_rollback_restores_a_previous_version()
    {
        var (svc, h) = Mappings();
        var rule = await svc.UpsertAsync(new UpsertMappingInput(
            null, ProviderType.AutotaskPsa, MappingScope.ProviderDefault, null,
            "status", "IN_PROGRESS", "status", "In Progress", MappingDirection.Bidirectional, false, null), "v1");

        // v1 snapshot now holds External = "In Progress". Change it.
        await svc.UpsertAsync(new UpsertMappingInput(
            rule.Id, ProviderType.AutotaskPsa, MappingScope.ProviderDefault, null,
            "status", "IN_PROGRESS", "status", "Working", MappingDirection.Bidirectional, false, null), "v2");
        (await h.Db.FieldMappings.SingleAsync()).ExternalValue.Should().Be("Working");

        var v1 = (await svc.VersionsAsync(ProviderType.AutotaskPsa, null)).Single(v => v.Version == 1);
        await svc.RollbackAsync(v1.Id);

        (await h.Db.FieldMappings.SingleAsync()).ExternalValue.Should().Be("In Progress"); // restored
        (await h.Db.AuditLog.CountAsync(a => a.Action == "mapping.rolledback")).Should().Be(1);
    }

    [Fact]
    public async Task Dead_lettered_job_can_be_reprocessed()
    {
        var h = AdminHarness.Create(Org);
        var audit = new AuditWriter(h.Db, h.User, h.Tenant, h.Clock);
        var svc = new JobMonitorService(h.Db, audit, h.Clock);

        var job = new BackgroundJob
        {
            MspOrganizationId = Org, JobType = "sync.inbound-event", PayloadJson = "{}",
            Status = BackgroundJobStatus.DeadLettered, Attempts = 5, LastError = "boom",
        };
        h.Db.BackgroundJobs.Add(job);
        await h.Db.SaveChangesAsync();

        await svc.ReprocessAsync(job.Id);

        job.Status.Should().Be(BackgroundJobStatus.Queued);
        job.Attempts.Should().Be(0);
        job.LastError.Should().BeNull();
        (await h.Db.AuditLog.CountAsync(a => a.Action == "job.reprocessed")).Should().Be(1);
    }

    [Fact]
    public async Task Reprocessing_a_non_dead_lettered_job_is_rejected()
    {
        var h = AdminHarness.Create(Org);
        var svc = new JobMonitorService(h.Db, new AuditWriter(h.Db, h.User, h.Tenant, h.Clock), h.Clock);
        var job = new BackgroundJob { MspOrganizationId = Org, JobType = "x", PayloadJson = "{}", Status = BackgroundJobStatus.Queued };
        h.Db.BackgroundJobs.Add(job);
        await h.Db.SaveChangesAsync();

        var act = async () => await svc.ReprocessAsync(job.Id);
        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Audit_query_returns_entries_for_the_current_tenant()
    {
        var h = AdminHarness.Create(Org);
        var audit = new AuditWriter(h.Db, h.User, h.Tenant, h.Clock);
        await audit.WriteAsync("test.action", "Thing", "1");

        var svc = new AuditQueryService(h.Db, h.Tenant);
        var entries = await svc.ListAsync();
        entries.Should().ContainSingle().Which.Action.Should().Be("test.action");
    }
}
