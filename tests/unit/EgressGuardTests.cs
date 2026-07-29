using System.Net;
using Desk.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class EgressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]        // loopback
    [InlineData("10.0.0.5")]         // private /8
    [InlineData("172.16.4.2")]       // private /12
    [InlineData("172.31.255.255")]   // private /12 upper bound
    [InlineData("192.168.1.10")]     // private /16
    [InlineData("169.254.169.254")]  // link-local — cloud metadata endpoint
    [InlineData("100.64.0.1")]       // CGNAT
    [InlineData("::1")]              // IPv6 loopback
    [InlineData("fe80::1")]          // IPv6 link-local
    [InlineData("fc00::1")]          // IPv6 unique-local
    public void Private_and_reserved_addresses_are_blocked(string ip)
        => EgressGuard.IsBlockedAddress(IPAddress.Parse(ip)).Should().BeTrue();

    [Theory]
    [InlineData("8.8.8.8")]          // public
    [InlineData("1.1.1.1")]          // public
    [InlineData("172.15.0.1")]       // just below private /12
    [InlineData("172.32.0.1")]       // just above private /12
    [InlineData("2606:4700:4700::1111")] // public IPv6
    public void Public_addresses_are_allowed(string ip)
        => EgressGuard.IsBlockedAddress(IPAddress.Parse(ip)).Should().BeFalse();
}
