using System.Net;
using System.Net.Sockets;
using Desk.PsaCore.Contracts;

namespace Desk.Infrastructure.Security;

/// <summary>
/// SSRF egress guard for connector HttpClients. Blocks requests whose host resolves to a loopback,
/// private, link-local, or otherwise reserved address so a misconfigured or malicious connection URL
/// cannot reach internal services (including the cloud metadata endpoint 169.254.169.254).
///
/// Off by default (self-hosted PSA instances may legitimately live on a private network); enable via
/// <c>Connectors:BlockPrivateEgress</c> and, where needed, an explicit host allowlist.
/// </summary>
public sealed class EgressGuard(ISet<string> allowedHosts) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var host = request.RequestUri?.Host;
        if (host is not null && !allowedHosts.Contains(host))
        {
            var addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, ct);

            if (addresses.Any(IsBlockedAddress))
                throw new ConnectorException(ConnectorFailureKind.InvalidRequest,
                    $"Blocked connector egress to a private/reserved host: {host}");
        }
        return await base.SendAsync(request, ct);
    }

    /// <summary>Pure classification of an address as private/reserved (not internet-routable).</summary>
    public static bool IsBlockedAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10                                   // 10.0.0.0/8
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254)                 // 169.254.0.0/16 link-local (metadata)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // 100.64.0.0/10 CGNAT
                || b[0] == 0;                                   // 0.0.0.0/8
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
            var b = ip.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC; // fc00::/7 unique-local
        }

        return false;
    }
}
