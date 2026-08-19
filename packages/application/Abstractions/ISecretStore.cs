namespace Desk.Application.Abstractions;

/// <summary>
/// Abstraction over the secret backend (AES-256-GCM-encrypted storage in Postgres; see
/// Desk.Infrastructure.Secrets.EncryptedDbSecretStore).
/// PSA credentials are written here and referenced from the database by an opaque path;
/// raw secret values never touch the DB, logs, or the frontend (spec §11 credential security).
/// </summary>
public interface ISecretStore
{
    /// <summary>Store a credential blob and return the opaque reference to persist on the connection row.</summary>
    Task<string> WriteAsync(string logicalName, IReadOnlyDictionary<string, string> data, CancellationToken ct = default);

    /// <summary>Resolve a previously stored credential blob by its reference. Callers must not log the result.</summary>
    Task<IReadOnlyDictionary<string, string>> ReadAsync(string secretRef, CancellationToken ct = default);

    /// <summary>
    /// Rotate/replace the secret at a reference, returning the reference it now lives at — normally
    /// the one passed in, but a NEW one when the old reference could not be reused.
    ///
    /// Callers must persist the returned value rather than assuming the reference is stable. A
    /// connection carried over from the Vault-era store holds a reference that never existed in
    /// this backend and is too long to become a key here, so re-entering its credentials has to
    /// mint a fresh reference; returning it is what lets the connection row be healed in the same
    /// save rather than pointing at something unreadable.
    /// </summary>
    Task<string> RotateAsync(string secretRef, IReadOnlyDictionary<string, string> data, CancellationToken ct = default);

    Task DeleteAsync(string secretRef, CancellationToken ct = default);
}
