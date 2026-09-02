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

    /// <summary>
    /// The canonical ticket-state hash, defined ONCE.
    ///
    /// Two callers must agree on it exactly: the sync hashes what arrived from the provider, and the
    /// portal hashes what it just wrote, so the sync can recognise its own change coming back. When
    /// each built its own field list, adding a field to one silently broke echo suppression in the
    /// other — every portal write then looked like a provider change. One function, no drift.
    /// </summary>
    public static string ForTicketState(
        string? status, string? priority, string? category, string? title, string? description,
        DateTimeOffset? resolvedAt, DateTimeOffset? closedAt, DateTimeOffset? slaDueAt)
        => Compute(new Dictionary<string, string?>
        {
            ["status"] = status,
            ["priority"] = priority,
            ["category"] = category,
            ["title"] = title,
            ["description"] = description,
            // Lifecycle dates belong here: a ticket can close in the PSA leaving every other field
            // identical, and an unchanged hash means the portal never records the closure at all.
            ["resolvedAt"] = resolvedAt?.ToString("O"),
            ["closedAt"] = closedAt?.ToString("O"),
            ["slaDueAt"] = slaDueAt?.ToString("O"),
        });
}
