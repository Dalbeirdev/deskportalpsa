namespace Desk.Infrastructure.Secrets;

/// <summary>
/// The at-rest row for <see cref="EncryptedDbSecretStore"/>. <see cref="Ciphertext"/> is the only
/// place a PSA credential exists outside memory — everything else in the schema references this
/// row by <see cref="Id"/> and never holds the plaintext.
///
/// Deliberately not a domain entity: it carries no <c>MspOrganizationId</c> and does not implement
/// <c>ITenantScoped</c>, so it sits outside <see cref="Persistence.DeskDbContext"/>'s tenant query
/// filter by design — the store is addressed purely by opaque reference, the same contract Vault
/// exposed.
/// </summary>
public sealed class SecretBlob
{
    /// <summary>The opaque reference callers persist as <c>PsaConnection.CredentialSecretRef</c>.</summary>
    public required string Id { get; set; }

    /// <summary>Free-text label (e.g. "ConnectWisePsa/Prod CW") kept only so a support session can
    /// tell which connection a row belongs to without decrypting it. Never used to look up a row.</summary>
    public required string LogicalName { get; set; }

    /// <summary>Nonce (12 bytes) + AES-256-GCM ciphertext + authentication tag (16 bytes), concatenated.</summary>
    public required byte[] Ciphertext { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
