using System.Security.Cryptography;
using System.Text;

namespace Desk.Application.Sync;

/// <summary>
/// Computes a stable hash of a normalized field set. Two uses:
///   - idempotency: an inbound update whose hash equals the ticket's stored hash is a no-op;
///   - echo detection: a change we pushed comes back with the same hash and is skipped.
/// Field order does not affect the result, so callers need not sort.
/// </summary>
public static class UpdateHasher
{
    // Multi-char delimiter unlikely to appear inside a mapped field key or value.
    private const string Separator = "|:@:|";
    private const string NullMarker = "<null>";

    public static string Compute(IEnumerable<KeyValuePair<string, string?>> fields)
    {
        var sb = new StringBuilder();
        foreach (var kv in fields.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.Append(kv.Key).Append('=').Append(kv.Value ?? NullMarker).Append(Separator);
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }
}
