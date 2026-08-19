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

        var returned = await store.RotateAsync(reference, new Dictionary<string, string> { ["PrivateKey"] = "new" });
        var read = await store.ReadAsync(reference);

        returned.Should().Be(reference, "an existing row keeps its reference so the connection row needs no change");
        read["PrivateKey"].Should().Be("new");
    }

    [Fact]
    public async Task Rotating_an_orphaned_reference_recovers_it_under_a_new_reference()
    {
        // The live production failure: a PsaConnection whose CredentialSecretRef survived the
        // Vault-in-dev-mode restart that discarded Vault's actual secret. "Edit the connection and
        // re-enter your credentials" — the fix the error message itself prescribes — rotates
        // against that orphaned reference, so it must succeed rather than throw.
        var (store, _) = Create();
        const string orphanedRef = "desk/psa-credentials/AutotaskPsa/Autotask/2a47afb201f94a759f7645f6b74a845b";

        var newRef = await store.RotateAsync(orphanedRef, new Dictionary<string, string> { ["Secret"] = "fresh-key" });

        newRef.Should().NotBe(orphanedRef, "the dead reference cannot be reused as a key");
        (await store.ReadAsync(newRef))["Secret"].Should().Be("fresh-key");
    }

    [Fact]
    public async Task A_recovered_reference_fits_the_column_the_real_database_declares()
    {
        // The bug the first attempt at this fix shipped: recovering the row AT the orphaned
        // reference looked right against the in-memory provider, which enforces no column widths,
        // and then failed in production with "value too long for type character varying(64)" —
        // Vault paths run past secret_blobs.Id. Asserting the width here is what makes this test
        // able to fail for the reason production did; the in-memory provider never will on its own.
        var (store, _) = Create();
        var orphanedRef = $"desk/psa-credentials/ConnectWisePsa/{new string('x', 120)}";

        var newRef = await store.RotateAsync(orphanedRef, new Dictionary<string, string> { ["Secret"] = "v" });

        newRef.Length.Should().BeLessThanOrEqualTo(64);
        (await store.ReadAsync(newRef))["Secret"].Should().Be("v");
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
