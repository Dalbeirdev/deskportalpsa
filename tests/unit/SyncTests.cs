using Desk.Application.Sync;
using Desk.Infrastructure.Sync;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class SyncTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();

    [Fact]
    public void Update_hash_is_order_independent()
    {
        var a = UpdateHasher.Compute(new Dictionary<string, string?> { ["status"] = "open", ["priority"] = "high" });
        var b = UpdateHasher.Compute(new Dictionary<string, string?> { ["priority"] = "high", ["status"] = "open" });
        a.Should().Be(b);
    }

    [Fact]
    public void Update_hash_changes_when_a_value_changes()
    {
        var a = UpdateHasher.Compute(new Dictionary<string, string?> { ["status"] = "open" });
        var b = UpdateHasher.Compute(new Dictionary<string, string?> { ["status"] = "closed" });
        a.Should().NotBe(b);
    }

    [Fact]
    public async Task Duplicate_event_is_rejected()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = TestDbContextFactory.ForPlatform(dbName);
        var store = new SyncEventStore(db, new TestClock());

        var reg = new SyncEventRegistration
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, EventType = "ticket.updated",
            IdempotencyKey = "evt-123", SourceMarker = SyncSource.Provider, OccurredAt = DateTimeOffset.UtcNow,
        };

        (await store.TryRegisterAsync(reg)).Should().BeTrue();   // first delivery processes
        (await store.TryRegisterAsync(reg)).Should().BeFalse();  // duplicate is skipped
    }

    [Fact]
    public async Task Portal_originated_change_is_detected_as_echo()
    {
        var dbName = Guid.NewGuid().ToString();
        var ticketId = Guid.NewGuid();
        var clock = new TestClock();
        await using var db = TestDbContextFactory.ForPlatform(dbName);
        var store = new SyncEventStore(db, clock);

        await store.TryRegisterAsync(new SyncEventRegistration
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, TicketId = ticketId,
            EventType = "ticket.updated", IdempotencyKey = "portal-1", SourceMarker = SyncSource.Portal,
            PayloadHash = "hash-abc", OccurredAt = clock.GetUtcNow(),
        });

        (await store.IsPortalEchoAsync(Conn, ticketId, "hash-abc")).Should().BeTrue();
        (await store.IsPortalEchoAsync(Conn, ticketId, "different-hash")).Should().BeFalse();
    }
}
