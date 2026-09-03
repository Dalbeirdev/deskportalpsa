using Desk.Application.Analytics;
using Desk.Application.Mapping;
using Desk.Application.Tickets;
using Desk.Connectors.Mock;
using Desk.Domain.Analytics;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Infrastructure.Analytics;
using Desk.Infrastructure.Persistence;
using Desk.Infrastructure.Sync;
using Desk.Infrastructure.Tickets;
using Desk.PsaCore.Contracts;
using Desk.PsaCore.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The activity log the productivity layer is built on. Two things decide whether it is worth
/// having: that portal actions and PSA observations stay distinguishable, and that recording never
/// interferes with the work it observes.
/// </summary>
public class ActivityEventTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid ClientUserId = Guid.NewGuid();

    private sealed class FakeResolver(IServiceManagementConnector c) : Desk.Application.Connectors.IConnectorResolver
    {
        public Task<IServiceManagementConnector> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(c);
    }

    private static async Task<DeskDbContext> SeedAsync()
    {
        var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        db.ClientCompanies.Add(new ClientCompany
        { Id = CompanyA, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "Acme", ExternalCompanyId = "176" });
        db.ClientUsers.Add(new ClientUser
        {
            Id = ClientUserId, MspOrganizationId = Org, ClientCompanyId = CompanyA,
            Email = "user@acme.test", DisplayName = "User A", IdpSubject = "sub-user",
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static ActivityRecorder Recorder(DeskDbContext db, TestClock clock)
        => new(db, new TenantContextFor(Org), clock, NullLogger<ActivityRecorder>.Instance);

    private sealed class TenantContextFor(Guid org) : Desk.Application.Abstractions.ITenantContext
    {
        public Guid? OrganizationId => org;
        public bool IsPlatformScope => false;
        public bool HasScope => true;
        public void SetTenant(Guid? organizationId) { }
        public void SetPlatformScope() { }
    }

    [Fact]
    public async Task A_staff_reply_records_portal_activity_attributed_to_the_person()
    {
        var clock = new TestClock();
        await using var db = await SeedAsync();
        var recorder = Recorder(db, clock);
        var svc = new TicketCommandService(db, new FakeResolver(new MockConnector(new MockConnectorOptions(), clock)),
            new MappingEngine(), new SyncEventStore(db, clock), new NoopTicketScopeQuery(), clock, recorder);
        var created = await svc.CreateAsync(new ClientAccess(Org, CompanyA, ClientUserId, false),
            new CreateTicketInput("Outlook down", null, null, null, null));
        var tech = Guid.NewGuid();

        await svc.AddStaffCommentAsync(tech, "Jane Tech", created.Id, "Looking now.", isPublic: true);

        var note = await db.ActivityEvents.SingleAsync(e => e.Kind == ActivityKind.NoteAdded);
        note.Source.Should().Be(ActivitySource.Portal, "the portal watched this happen");
        note.ActorUserId.Should().Be(tech);
        note.ClientCompanyId.Should().Be(CompanyA, "client analytics must not need a join back to tickets");
    }

    [Fact]
    public async Task A_closure_the_psa_reports_is_recorded_as_psa_activity_once()
    {
        // Re-importing an already-closed ticket must not emit a fresh closure every sync, or a
        // single closure is counted as many.
        var clock = new TestClock();
        await using var db = await SeedAsync();
        var recorder = Recorder(db, clock);
        var sync = new TicketSyncService(db, new MappingEngine(), new SyncEventStore(db, clock), clock, recorder);
        var closed = new DateTimeOffset(2026, 6, 3, 15, 0, 0, TimeSpan.Zero);
        UnifiedTicket Incoming(DateTimeOffset? closedAt, string title = "Email Issue") => new()
        {
            ExternalId = "7814", Title = title, Status = "New",
            RequesterExternalId = "176", ClosedAt = closedAt, ResolvedAt = closedAt,
        };

        await sync.UpsertFromProviderAsync(Conn, Incoming(null), []);
        await sync.UpsertFromProviderAsync(Conn, Incoming(closed), []);
        // The third pass must CHANGE something, or the update hash short-circuits before the
        // emission code is even reached and the de-duplication guard is never exercised — the
        // test would then pass with the guard deleted, which is no test at all.
        await sync.UpsertFromProviderAsync(Conn, Incoming(closed, "Email Issue (renamed in the PSA)"), []);

        var closures = await db.ActivityEvents.Where(e => e.Kind == ActivityKind.TicketClosed).ToListAsync();
        closures.Should().ContainSingle("the closure happened once, however many times it is re-read");
        closures[0].Source.Should().Be(ActivitySource.Psa, "we infer this; we did not watch it");
        closures[0].OccurredAt.Should().Be(closed, "the PSA's own timestamp, not when we noticed");
    }

    [Fact]
    public async Task Activity_is_timestamped_when_it_happened_not_when_it_was_written()
    {
        // Technicians log Friday's work on Monday. Aggregating by row-creation makes every Monday
        // look heroic and every Friday look idle.
        var clock = new TestClock();
        await using var db = await SeedAsync();
        var friday = new DateTimeOffset(2026, 8, 28, 16, 0, 0, TimeSpan.Zero);

        await Recorder(db, clock).RecordAsync(new ActivityRecord(ActivityKind.TimeLogged, ActivitySource.Portal)
        {
            MspOrganizationId = Org,
            OccurredAt = friday,
            DurationSeconds = 1800,
        });

        var e = await db.ActivityEvents.SingleAsync();
        e.OccurredAt.Should().Be(friday);
        e.CreatedAt.Should().NotBe(friday, "the row timestamp stays the row timestamp");
    }

    [Fact]
    public async Task Recording_never_fails_the_work_it_observes()
    {
        // Telemetry is not the job. A reply that reached the PSA must not report failure because
        // the row describing it could not be written.
        var clock = new TestClock();
        await using var db = await SeedAsync();
        await db.DisposeAsync(); // the store is now unusable, exactly as an outage would leave it

        var act = async () => await Recorder(db, clock).RecordAsync(
            new ActivityRecord(ActivityKind.NoteAdded, ActivitySource.Portal) { MspOrganizationId = Org });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_event_with_no_tenant_is_dropped_rather_than_written_invisible()
    {
        // The global filter means a tenantless row could never be read back — it would cost storage
        // and never appear in an answer, which is worse than not writing it.
        var clock = new TestClock();
        await using var db = await SeedAsync();
        var recorder = new ActivityRecorder(db, new NoTenant(), clock, NullLogger<ActivityRecorder>.Instance);

        await recorder.RecordAsync(new ActivityRecord(ActivityKind.NoteAdded, ActivitySource.Portal));

        (await db.ActivityEvents.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    private sealed class NoTenant : Desk.Application.Abstractions.ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => false;
        public bool HasScope => false;
        public void SetTenant(Guid? organizationId) { }
        public void SetPlatformScope() { }
    }
}
