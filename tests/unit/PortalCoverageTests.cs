using Desk.Application.Analytics;
using Desk.Domain.Analytics;
using Desk.Domain.Enums;
using Desk.Domain.Identity;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Analytics;
using Desk.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Portal coverage: how much of the work the PSA recorded is visible here.
///
/// The tests that matter most are about what the number must NOT claim. A range before the activity
/// log existed is an absence of evidence, not a low score, and presenting the two the same way is
/// how a rollout metric becomes an accusation.
/// </summary>
public class PortalCoverageTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly DateTimeOffset Day1 = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private static async Task<DeskDbContext> SeedAsync()
    {
        var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "AT", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
        });
        db.ClientCompanies.Add(new ClientCompany
        { Id = Company, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "Acme", ExternalCompanyId = "1" });
        await db.SaveChangesAsync();
        return db;
    }

    private static Guid AddTicket(DeskDbContext db, string ext)
    {
        var id = Guid.NewGuid();
        db.Tickets.Add(new Ticket
        {
            Id = id, MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.AutotaskPsa,
            ExternalTicketId = ext, ClientCompanyId = Company, RequesterName = "r", RequesterEmail = "r@a.test",
            Title = ext, PortalStatus = "NEW", PortalPriority = "NORMAL",
        });
        return id;
    }

    private static void AddEntry(DeskDbContext db, Guid ticketId, string tech, decimal hours, DateTimeOffset when)
        => db.TicketTimeEntries.Add(new TicketTimeEntry
        {
            MspOrganizationId = Org, TicketId = ticketId, Hours = hours, Billable = true,
            TechnicianExternalId = tech, EntryDate = when,
            Source = TimeEntrySource.Provider, SyncStatus = TimeEntrySyncStatus.Synced,
        });

    private static void AddPortalEvent(DeskDbContext db, Guid ticketId, DateTimeOffset when, Guid? actor = null)
        => db.ActivityEvents.Add(new ActivityEvent
        {
            MspOrganizationId = Org, OccurredAt = when, Source = ActivitySource.Portal,
            Kind = ActivityKind.NoteAdded, TicketId = ticketId, ClientCompanyId = Company, ActorUserId = actor,
        });

    [Fact]
    public async Task Coverage_is_the_share_of_psa_work_with_matching_portal_activity()
    {
        await using var db = await SeedAsync();
        var t1 = AddTicket(db, "1");
        var t2 = AddTicket(db, "2");
        AddEntry(db, t1, "R1", 2m, Day1);
        AddEntry(db, t2, "R1", 3m, Day1);
        AddPortalEvent(db, t1, Day1.AddHours(1));     // only t1 was worked in the portal
        await db.SaveChangesAsync();

        var report = await new PortalCoverageService(db).CoverageAsync(new MetricsFilter());

        report.TotalPsaEntries.Should().Be(2);
        report.TotalCorroborated.Should().Be(1);
        report.OverallCoveragePct.Should().Be(50);
        report.TotalPsaHours.Should().Be(5m, "the PSA's hours are reported as they are, never adjusted");
    }

    [Fact]
    public async Task Activity_on_a_different_day_does_not_corroborate()
    {
        // Same ticket, different day is different work. Matching on ticket alone would inflate
        // coverage for any long-running ticket touched once.
        await using var db = await SeedAsync();
        var t1 = AddTicket(db, "1");
        AddEntry(db, t1, "R1", 2m, Day1);
        AddPortalEvent(db, t1, Day1.AddDays(3));
        await db.SaveChangesAsync();

        var report = await new PortalCoverageService(db).CoverageAsync(new MetricsFilter());

        report.TotalCorroborated.Should().Be(0);
        report.OverallCoveragePct.Should().Be(0);
    }

    [Fact]
    public async Task A_range_beginning_before_the_log_existed_is_flagged_as_such()
    {
        // THE test. The activity store started today, so any historical range would otherwise show
        // near-zero coverage and read as a damning finding about people rather than about the log.
        await using var db = await SeedAsync();
        var t1 = AddTicket(db, "1");
        AddEntry(db, t1, "R1", 2m, Day1.AddDays(-30));
        AddPortalEvent(db, t1, Day1);
        await db.SaveChangesAsync();

        var report = await new PortalCoverageService(db)
            .CoverageAsync(new MetricsFilter { From = Day1.AddDays(-60) });

        report.ActivityRecordedSince.Should().Be(Day1);
        report.RangeStartsBeforeRecording.Should().BeTrue(
            "the surface must be able to say this is missing evidence, not low coverage");
    }

    [Fact]
    public async Task A_range_entirely_within_the_recorded_period_is_not_flagged()
    {
        await using var db = await SeedAsync();
        var t1 = AddTicket(db, "1");
        AddEntry(db, t1, "R1", 2m, Day1.AddDays(1));
        AddPortalEvent(db, t1, Day1);
        await db.SaveChangesAsync();

        var report = await new PortalCoverageService(db)
            .CoverageAsync(new MetricsFilter { From = Day1.AddHours(1) });

        report.RangeStartsBeforeRecording.Should().BeFalse();
    }

    [Fact]
    public async Task No_psa_work_reports_null_coverage_rather_than_zero()
    {
        // Zero reads as a finding — "nothing is visible" — when the truth is that nothing happened.
        await using var db = await SeedAsync();

        var report = await new PortalCoverageService(db).CoverageAsync(new MetricsFilter());

        report.OverallCoveragePct.Should().BeNull();
        report.TotalPsaEntries.Should().Be(0);
    }

    [Fact]
    public async Task Rows_carry_the_technicians_name_and_their_portal_event_count()
    {
        await using var db = await SeedAsync();
        var user = Guid.NewGuid();
        db.AppUsers.Add(new AppUser { Id = user, MspOrganizationId = Org, Email = "h@t.test", DisplayName = "Harpal" });
        db.UserPsaIdentities.Add(new UserPsaIdentity
        {
            MspOrganizationId = Org, AppUserId = user, PsaConnectionId = Conn,
            ExternalTechnicianId = "R1", ExternalTechnicianName = "Harpal Singh",
        });
        var t1 = AddTicket(db, "1");
        AddEntry(db, t1, "R1", 4m, Day1);
        AddPortalEvent(db, t1, Day1.AddMinutes(30), actor: user);
        await db.SaveChangesAsync();

        var report = await new PortalCoverageService(db).CoverageAsync(new MetricsFilter());

        var row = report.Technicians.Single();
        row.TechnicianName.Should().Be("Harpal Singh");
        row.PsaHours.Should().Be(4m);
        row.CoveragePct.Should().Be(100);
        row.PortalEvents.Should().Be(1);
    }
}
