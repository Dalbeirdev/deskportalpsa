using Desk.Application.Common;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Connectors;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Tenancy;
using Desk.PsaCore.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// A factory whose CreateAsync throws whatever the caller wants — used here to simulate what
/// EncryptedDbSecretStore/InMemorySecretStore throw when a stored CredentialSecretRef no longer
/// resolves (the exact failure mode after the Vault outage: connections whose secret was lost).
/// </summary>
file sealed class ThrowingFactory(ProviderType provider, Exception toThrow) : IConnectorFactory
{
    public ProviderType Provider => provider;
    public Task<IServiceManagementConnector> CreateAsync(Guid psaConnectionId, CancellationToken ct = default)
        => throw toThrow;
}

public class ConnectorResolverTests
{
    private static DeskDbContext NewDb(Guid org)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(org);
        var options = new DbContextOptionsBuilder<DeskDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DeskDbContext(options, tenant, new TestClock());
    }

    [Fact]
    public async Task A_missing_secret_reference_becomes_an_actionable_message_not_a_generic_500()
    {
        var org = Guid.NewGuid();
        var db = NewDb(org);
        var connection = new PsaConnection
        {
            Name = "Prod CW", Provider = ProviderType.ConnectWisePsa, ApiEndpoint = "https://cw.local",
            CredentialSecretRef = "gone", MspOrganizationId = org, IsEnabled = true,
        };
        db.PsaConnections.Add(connection);
        await db.SaveChangesAsync();

        var factory = new ThrowingFactory(ProviderType.ConnectWisePsa, new KeyNotFoundException("Secret reference not found."));
        var resolver = new ConnectorResolver(db, [factory]);

        var act = () => resolver.ResolveAsync(connection.Id);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Message.Should().Contain("Prod CW").And.Contain("re-enter them");
    }

    [Fact]
    public async Task Other_failures_from_the_factory_are_not_reinterpreted()
    {
        // Only a missing secret reference gets the friendlier message — every other failure
        // (a bad HTTP call, a malformed connector config) should reach the caller unchanged.
        var org = Guid.NewGuid();
        var db = NewDb(org);
        var connection = new PsaConnection
        {
            Name = "Prod AT", Provider = ProviderType.AutotaskPsa, ApiEndpoint = "https://at.local",
            CredentialSecretRef = "ref", MspOrganizationId = org, IsEnabled = true,
        };
        db.PsaConnections.Add(connection);
        await db.SaveChangesAsync();

        var factory = new ThrowingFactory(ProviderType.AutotaskPsa, new InvalidOperationException("boom"));
        var resolver = new ConnectorResolver(db, [factory]);

        var act = () => resolver.ResolveAsync(connection.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
