using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Desk.Application.Attachments;

namespace Desk.Infrastructure.Attachments;

public sealed class AttachmentStorageOptions
{
    public string SigningKey { get; init; } = "dev-attachment-signing-key";
    public string PublicBaseUrl { get; init; } = "http://localhost:5080";
}

/// <summary>
/// In-memory object storage with genuinely tamper-evident, time-limited download URLs (HMAC over
/// key + expiry). Production swaps this for a MinIO/S3 impl behind the same interface; the signed-URL
/// contract and the blob endpoint that validates it stay identical.
/// </summary>
public sealed class InMemoryObjectStorage(AttachmentStorageOptions options, TimeProvider clock) : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _store = new();

    public Task PutAsync(string key, byte[] data, string contentType, CancellationToken ct = default)
    {
        _store[key] = (data, contentType);
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(key, out var v) ? v.Data : null);

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<string> PresignGetAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var exp = clock.GetUtcNow().Add(ttl).ToUnixTimeSeconds();
        var sig = Sign(key, exp, options.SigningKey);
        var url = $"{options.PublicBaseUrl}/api/attachments/blob?key={Uri.EscapeDataString(key)}&exp={exp}&sig={sig}";
        return Task.FromResult(url);
    }

    /// <summary>Validates a presigned request: signature intact and not expired.</summary>
    public static bool VerifySignature(string key, long exp, string sig, string signingKey, DateTimeOffset now)
    {
        if (exp < now.ToUnixTimeSeconds()) return false;
        var expected = Sign(key, exp, signingKey);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sig));
    }

    private static string Sign(string key, long exp, string signingKey)
        => Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes($"{key}|{exp}")));
}
