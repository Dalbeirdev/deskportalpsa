using Desk.Infrastructure.Admin;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The logo URL is stored from admin input and rendered as an image source, so what it refuses
/// matters more than what it accepts. Rejecting hostile schemes at the point of storage means no
/// rendering site has to remember to.
/// </summary>
public class ConnectionLogoUrlTests
{
    [Theory]
    [InlineData("https://cdn.example.com/autotask.svg")]
    [InlineData("http://intranet.local/logo.png")]
    public void An_absolute_http_url_is_kept(string url)
        => ConnectionAdminService.NormaliseLogoUrl(url).Should().Be(url);

    [Fact]
    public void A_site_relative_path_is_kept_so_a_logo_can_be_served_by_the_portal_itself()
        => ConnectionAdminService.NormaliseLogoUrl("/brand/autotask.svg").Should().Be("/brand/autotask.svg");

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    [InlineData("//evil.example/logo.png")]   // protocol-relative: silently borrows the page scheme
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url at all")]
    public void Anything_that_is_not_a_plain_http_or_relative_reference_is_dropped(string url)
        => ConnectionAdminService.NormaliseLogoUrl(url).Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_input_clears_the_logo_rather_than_storing_empty_text(string? url)
        => ConnectionAdminService.NormaliseLogoUrl(url).Should().BeNull();

    [Fact]
    public void An_absurdly_long_value_is_refused_rather_than_truncated_into_a_broken_url()
        => ConnectionAdminService.NormaliseLogoUrl("https://example.com/" + new string('a', 600))
            .Should().BeNull();

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
        => ConnectionAdminService.NormaliseLogoUrl("  https://example.com/a.png  ")
            .Should().Be("https://example.com/a.png");
}
