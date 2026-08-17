using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Secrets;
using Desk.Infrastructure.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

public class EncryptedDbSecretStoreTests
{
    private const string TestKey = "w+WEoJiLQLVmZzgEm//uVd0YpeTwnhwm2rUyftBqdO8=";

    private static (EncryptedDbSecretStore Store, DeskDbContext Db) Create(string? dbName = null)
    {
        var tenant = new TenantContext();
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<DeskDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var db = new DeskDbContext(options, tenant, clock);
        var cipher = new SecretCipher(new SecretEncryptionOptions { Key = TestKey });
        return (new EncryptedDbSecretStore(db, cipher, clock), db);
    }

    [Fact]
    public async Task Write_then_read_returns_the_original_credential_fields()
    {
        var (store, _) = Create();
        var creds = new Dictionary<string, string> { ["CompanyId"] = "acme", ["PrivateKey"] = "topsecret" };

        var reference = await store.WriteAsync("ConnectWisePsa/Prod CW", creds);
        var read = await store.ReadAsync(reference);

        read.Should().BeEquivalentTo(creds);
    }

    [Fact]
    public async Task The_row_never_holds_plaintext()
    {
        // What actually protects a credential once Vault is out of the picture: the bytes sitting
        // in Postgres must never contain the secret in the clear, tenant filter or not.
        var (store, db) = Create();
        var reference = await store.WriteAsync("Autotask/Prod AT", new Dictionary<string, string>
        {
            ["Secret"] = "super-secret-value",
        });

        var blob = await db.Set<SecretBlob>().AsNoTracking().SingleAsync(b => b.Id == reference);
        var raw = System.Text.Encoding.Latin1.GetString(blob.Ciphertext);

        raw.Should().NotContain("super-secret-value");
    }

    [Fact]
    public async Task Rotate_replaces_the_value_at_the_same_reference()
    {
        var (store, _) = Create();
        var reference = await store.WriteAsync("ConnectWisePsa/Prod CW", new Dictionary<string, string> { ["PrivateKey"] = "old" });

        await store.RotateAsync(reference, new Dictionary<string, string> { ["PrivateKey"] = "new" });
        var read = await store.ReadAsync(reference);

        read["PrivateKey"].Should().Be("new");
    }

    [Fact]
    public async Task Delete_makes_the_reference_unreadable()
    {
        var (store, _) = Create();
        var reference = await store.WriteAsync("ConnectWisePsa/Prod CW", new Dictionary<string, string> { ["PrivateKey"] = "x" });

        await store.DeleteAsync(reference);

        var act = () => store.ReadAsync(reference);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Deleting_an_already_deleted_reference_does_not_throw()
    {
        var (store, _) = Create();
        var reference = await store.WriteAsync("ConnectWisePsa/Prod CW", new Dictionary<string, string> { ["PrivateKey"] = "x" });
        await store.DeleteAsync(reference);

        var act = () => store.DeleteAsync(reference);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Reading_an_unknown_reference_throws()
    {
        var (store, _) = Create();

        var act = () => store.ReadAsync("does-not-exist");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
