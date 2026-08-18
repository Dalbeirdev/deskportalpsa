using System.Text.Json;
using Desk.Application.Abstractions;
using Desk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Desk.Infrastructure.Secrets;

/// <summary>
/// PSA credentials, encrypted at rest in the same Postgres database as everything else.
///
/// Replaces the dev-mode Vault container this deployment ran with: Vault's in-memory backend
/// discards every secret on restart, and a restart is not a rare event on a shared host — it took
/// out both live PSA connections after ~26 hours with no warning beyond the sync job going quiet.
/// Postgres already has a real volume and a WAL; encrypting a column there needs no separate
/// process to keep alive, no unseal step after a reboot, and no second thing that can lose state
/// independently of the database everything else already depends on.
///
/// The <see cref="ISecretStore"/> contract is unchanged, so <c>PsaConnection.CredentialSecretRef</c>
/// keeps meaning exactly what it meant before: an opaque string a caller must resolve through this
/// interface, never a value that can be read off the row.
/// </summary>
public sealed class EncryptedDbSecretStore(DeskDbContext db, SecretCipher cipher, TimeProvider clock) : ISecretStore
{
    public async Task<string> WriteAsync(string logicalName, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var blob = new SecretBlob
        {
            Id = Guid.NewGuid().ToString("N"),
            LogicalName = logicalName,
            Ciphertext = cipher.Encrypt(Serialize(data)),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Set<SecretBlob>().Add(blob);
        await db.SaveChangesAsync(ct);
        return blob.Id;
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadAsync(string secretRef, CancellationToken ct = default)
    {
        var blob = await db.Set<SecretBlob>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == secretRef, ct)
            ?? throw new KeyNotFoundException("Secret reference not found.");
        return Deserialize(cipher.Decrypt(blob.Ciphertext));
    }

    public async Task RotateAsync(string secretRef, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        // Upsert, not update-only: a PsaConnection can hold a CredentialSecretRef with no backing
        // row here — the Vault-era connections this store replaced lost their secrets to an
        // in-memory backend wiped by a container restart (see this class's own doc comment), and
        // their CredentialSecretRef still names the old Vault key. "Edit the connection and
        // re-enter your credentials" is the documented, self-service fix for exactly that — and it
        // must not itself throw. Recreating the row AT THE SAME Id keeps that ref valid without
        // requiring the caller to persist a freshly minted one. The other two ISecretStore
        // implementations (InMemory/File) already treat Rotate as upsert; this one shouldn't be
        // the odd one out.
        var blob = await db.Set<SecretBlob>().FirstOrDefaultAsync(b => b.Id == secretRef, ct);
        var now = clock.GetUtcNow();
        if (blob is null)
        {
            blob = new SecretBlob
            {
                Id = secretRef, LogicalName = $"recovered:{secretRef}",
                Ciphertext = cipher.Encrypt(Serialize(data)), CreatedAt = now, UpdatedAt = now,
            };
            db.Set<SecretBlob>().Add(blob);
        }
        else
        {
            blob.Ciphertext = cipher.Encrypt(Serialize(data));
            blob.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string secretRef, CancellationToken ct = default)
    {
        var blob = await db.Set<SecretBlob>().FirstOrDefaultAsync(b => b.Id == secretRef, ct);
        if (blob is null) return; // matches the Vault-backed store: deleting an already-gone secret is not an error.
        db.Set<SecretBlob>().Remove(blob);
        await db.SaveChangesAsync(ct);
    }

    private static byte[] Serialize(IReadOnlyDictionary<string, string> data) => JsonSerializer.SerializeToUtf8Bytes(data);

    private static IReadOnlyDictionary<string, string> Deserialize(byte[] json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("Decrypted secret did not contain a credential object.");
}
