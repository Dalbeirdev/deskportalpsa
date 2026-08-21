using System.Security.Claims;
using Desk.Api.Auth;
using Desk.Domain.Authorization;
using Desk.Domain.Identity;
using Desk.Domain.Tenancy;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// A PURE client login (no AppUser) must come out of claims transformation able to use the client
/// portal: org claim for the tenant filter, and exactly the two permissions the client-reachable
/// endpoints gate on — no more. Before this branch existed, a client-only login authenticated and
/// then hit a wall (zero claims), so only people who were ALSO staff could use the client portal.
/// </summary>
public class ClientLoginTests
{
    private static readonly Guid Org = Guid.NewGuid();

    private static ClaimsPrincipal Principal(string subject, string? email = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject) };
        if (email is not null) claims.Add(new(ClaimTypes.Email, email));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClientUser Client(string? subject, string email = "harpal@client.test") => new()
    {
        MspOrganizationId = Org,
        ClientCompanyId = Guid.NewGuid(),
        Email = email,
        DisplayName = "Harpal Singh",
        IdpSubject = subject,
        IsCompanyAdministrator = true,
    };

    [Fact]
    public async Task A_pure_client_login_gets_org_scope_and_exactly_the_client_permissions()
    {
        await using var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        db.ClientUsers.Add(Client("sub-harpal"));
        await db.SaveChangesAsync();

        var result = await new DeskClaimsTransformation(db, new TestClock()).TransformAsync(Principal("sub-harpal"));

        result.FindFirstValue(CurrentUser.OrgClaim).Should().Be(Org.ToString());
        result.FindAll(CurrentUser.PermissionClaim).Select(c => c.Value).Should()
            .BeEquivalentTo([Permissions.TicketsCreate, Permissions.TicketsAddPublicNote],
                "growing the client claim set is a security decision, not a convenience");
        result.FindFirstValue(CurrentUser.UserIdClaim).Should().BeNull(
            "desk_uid is the STAFF id space — staff fallbacks must keep seeing 'no staff identity' for a client");
    }

    [Fact]
    public async Task An_invited_client_binds_by_verified_email_once()
    {
        await using var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        db.ClientUsers.Add(Client(subject: null, email: "harpal@client.test"));
        await db.SaveChangesAsync();

        var xform = new DeskClaimsTransformation(db, new TestClock());
        var result = await xform.TransformAsync(Principal("sub-new", "Harpal@Client.TEST"));
        result.FindFirstValue(CurrentUser.OrgClaim).Should().Be(Org.ToString(), "case-insensitive email bind");

        // Bound by subject now: a DIFFERENT subject with the same email must not steal the account.
        var thief = await new DeskClaimsTransformation(db, new TestClock())
            .TransformAsync(Principal("sub-thief", "harpal@client.test"));
        thief.FindFirstValue(CurrentUser.OrgClaim).Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_subject_gains_nothing()
    {
        await using var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        var result = await new DeskClaimsTransformation(db, new TestClock()).TransformAsync(Principal("sub-nobody"));
        result.FindFirstValue(CurrentUser.OrgClaim).Should().BeNull();
        result.FindAll(CurrentUser.PermissionClaim).Should().BeEmpty();
    }

    [Fact]
    public async Task A_dual_identity_resolves_as_staff_not_client()
    {
        await using var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        var clock = new TestClock();
        db.AppUsers.Add(new AppUser
        {
            MspOrganizationId = Org, Email = "harpal@client.test", DisplayName = "Harpal Singh",
            IdpSubject = "sub-dual", IsActive = true,
            // Fresh, so the staff branch skips its LastActiveAt stamp — ExecuteUpdateAsync is not
            // supported by the in-memory provider, and this test is about identity, not activity.
            LastActiveAt = clock.GetUtcNow(),
        });
        db.ClientUsers.Add(Client("sub-dual"));
        await db.SaveChangesAsync();

        var result = await new DeskClaimsTransformation(db, clock).TransformAsync(Principal("sub-dual"));

        result.FindFirstValue(CurrentUser.UserIdClaim).Should().NotBeNull(
            "an AppUser with the same subject keeps its staff identity — client resolution is the fallback, not the override");
    }
}
