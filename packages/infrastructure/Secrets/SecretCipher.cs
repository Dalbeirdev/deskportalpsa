using System.Security.Cryptography;

namespace Desk.Infrastructure.Secrets;

public sealed class SecretEncryptionOptions
{
    /// <summary>Base64-encoded 256-bit key. Generate with <c>openssl rand -base64 32</c>.
    /// Losing this key makes every stored PSA credential permanently unreadable — back it up
    /// with the same care as the database itself, and never regenerate it while secrets exist.</summary>
    public string Key { get; set; } = "";
}

/// <summary>
/// AES-256-GCM over a single master key. One primitive, not a key-management system: the master
/// key comes from configuration (ultimately the host's <c>.env.prod</c>, outside the database) and
/// every row gets its own random nonce, so this needs no key rotation machinery to be safe — GCM's
/// authentication tag also means a tampered or corrupted row fails loudly on read instead of
/// decrypting to garbage.
/// </summary>
public sealed class SecretCipher
{
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly byte[] _key;

    public SecretCipher(SecretEncryptionOptions options)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(options.Key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Secrets:EncryptionKey is not valid base64. Generate one with: openssl rand -base64 32");
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException(
                $"Secrets:EncryptionKey must decode to 32 bytes (AES-256), got {key.Length}. " +
                "Generate one with: openssl rand -base64 32");
        }

        _key = key;
    }

    public byte[] Encrypt(byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];

        using var aes = new AesGcm(_key, TagBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var combined = new byte[NonceBytes + ciphertext.Length + TagBytes];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceBytes);
        Buffer.BlockCopy(ciphertext, 0, combined, NonceBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, NonceBytes + ciphertext.Length, TagBytes);
        return combined;
    }

    /// <summary>Throws <see cref="CryptographicException"/> if the row was tampered with, corrupted,
    /// or encrypted under a different key.</summary>
    public byte[] Decrypt(byte[] combined)
    {
        if (combined.Length < NonceBytes + TagBytes)
            throw new CryptographicException("Stored secret is shorter than a valid nonce + tag.");

        var nonce = combined.AsSpan(0, NonceBytes);
        var tag = combined.AsSpan(combined.Length - TagBytes, TagBytes);
        var ciphertext = combined.AsSpan(NonceBytes, combined.Length - NonceBytes - TagBytes);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
