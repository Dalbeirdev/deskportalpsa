using Desk.Application.Analytics;
using Desk.Domain.Enums;
using Desk.Domain.Tenancy;
using Desk.Domain.Tickets;
using Desk.Infrastructure.Analytics;
using Desk.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Where the desk's capacity goes, by client. What is under test is mostly what the service
/// REFUSES to do: no estimating, no averaging over whatever happened to be present, and no
/// measuring a ticket's age from the day the portal imported it.
/// </summary>
public class ClientWorkloadTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Conn = Guid.NewGuid();
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Globex = Guid.NewGuid();

    private static async Task<DeskDbContext> SeedAsync()
    {
        var db = TestDbContextFactory.ForPlatform(Guid.NewGuid().ToString());
        db.PsaConnections.Add(new PsaConnection
        {
            Id = Conn, MspOrganizationId = Org, Name = "Autotask", Provider = ProviderType.AutotaskPsa,
            ApiEndpoint = "https://x", CredentialSecretRef = "mem://x",
            ImportClosedTickets = true, FilterActiveWithinDays = 90,
        });
        db.ClientCompanies.Add(new ClientCompany
        { Id = Acme, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "Acme", ExternalCompanyId = "1" });
        db.ClientCompanies.Add(new ClientCompany
        { Id = Globex, MspOrganizationId = Org, PsaConnectionId = Conn, Name = "Globex", ExternalCompanyId = "2" });
        await db.SaveChangesAsync();
        return db;
    }

    private static Ticket T(Guid company, string ext, DateTimeOffset? raised = null, DateTimeOffset? closed = null,
        DateTimeOffset? slaDue = null, string? tech = null, decimal hours = 0, decimal billable = 0) => new()
        {
            MspOrganizationId = Org, PsaConnectionId = Conn, Provider = ProviderType.AutotaskPsa,
            ExternalTicketId = ext, ClientCompanyId = company,
            RequesterName = "r", RequesterEmail = "r@a.test", Title = ext,
            PortalStatus = "NEW", PortalPriority = "NORMAL",
            PsaCreatedAt = raised, ClosedAt = closed, SlaDueAt = slaDue,
            AssignedTechnicianExternalId = tech, TimeWorkedHours = hours, BillableHours = billable,
        };

    private static readonly DateTimeOffset Jun1 = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Clients_are_ranked_by_the_capacity_they_consume()
    {
        // Hours, not ticket count: the question is where the team's time goes, and twenty trivial
        // tickets are not the same drain as three that took a day each.
        await using var db = await SeedAsync();
        db.Tickets.AddRange(
            T(Acme, "1", Jun1, hours: 2),
            T(Globex, "2", Jun1, hours: 9),
            T(Globex, "3", Jun1, hours: 1));
        await db.SaveChangesAsync();

        var report = await new ClientWorkloadService(db).ForClientsAsync(new MetricsFilter());

        report.Clients.Select(c => c.ClientName).Should().Equal("Globex", "Acme");
        report.Clients[0].HoursWorked.Should().Be(10);
        report.Clients[0].TotalTickets.Should().Be(2);
    }

    [Fact]
    public async Task Resolution_time_is_measured_from_the_psa_raise_date()
    {
        // Two days of real elapsed time. Measured from the row's own timestamp it would be minutes,
        // because that is when the test — like the portal — created the row.
        await using var db = await SeedAsync();
        db.Tickets.Add(T(Acme, "1", raised: Jun1, closed: Jun1.AddDays(2)));
        await db.SaveChangesAsync();

        var report = await new ClientWorkloadService(db).ForClientsAsync(new MetricsFilter());

        report.Clients.Single().AvgResolutionHours.Should().Be(48);
    }

    [Fact]
    public async Task Tickets_without_a_closure_are_excluded_from_the_average_and_counted()
    {
        // An average over two of five is not wrong, but shown alone it reads as the whole picture.
        // The sample size travels with the figure so the surface can say what it measured.
        await using var db = await SeedAsync();
        db.Tickets.AddRange(
            T(Acme, "1", raised: Jun1, closed: Jun1.AddHours(10)),
            T(Acme, "2", raised: Jun1, closed: Jun1.AddHours(20)),
            T(Acme, "3", raised: Jun1),
            T(Acme, "4", raised: Jun1),
            T(Acme, "5", raised: Jun1));
        await db.SaveChangesAsync();

        var report = await new ClientWorkloadService(db).ForClientsAsync(new MetricsFilter());

        var acme = report.Clients.Single();
        acme.AvgResolutionHours.Should().Be(15, "only the two that closed can be averaged");
        acme.ResolutionSample.Should().Be(2);
        acme.OpenTickets.Should().Be(3);
        report.TicketsWithoutClosure.Should().Be(3);
    }

    [Fact]
    public async Task Sla_compliance_counts_only_tickets_that_had_a_target()
    {
        // A ticket with no SLA target is not a breach and not a success — it is not evidence, and
        // folding it in either direction would invent a number.
        await using var db = await SeedAsync();
        db.Tickets.AddRange(
            T(Acme, "1", raised: Jun1, closed: Jun1.AddHours(2), slaDue: Jun1.AddHours(4)),   // met
            T(Acme, "2", raised: Jun1, closed: Jun1.AddHours(9), slaDue: Jun1.AddHours(4)),   // missed
            T(Acme, "3", raised: Jun1, closed: Jun1.AddHours(1)));                            // no target
        await db.SaveChangesAsync();

        var report = await new ClientWorkloadService(db).ForClientsAsync(new MetricsFilter());

        var acme = report.Clients.Single();
        acme.SlaEligible.Should().Be(2);
        acme.SlaCompliancePct.Should().Be(50);
    }

    [Fact]
    public async Task A_client_with_no_sla_data_reports_null_rather_than_a_hundred_percent()
    {
        // The failure mode this pins: zero eligible tickets dividing to "100% compliant", which
        // reads as excellent and means nothing was measured.
        await using var db = await SeedAsync();
        db.Tickets.Add(T(Acme, "1", raised: Jun1, closed: Jun1.AddHours(3)));
        await db.SaveChangesAsync();

        var report = await new ClientWorkloadService(db).ForClientsAsync(new MetricsFilter());

        report.Clients.Single().SlaCompliancePct.Should().BeNull();
        report.Clients.Single().SlaEligible.Should().Be(0);
    }

    [Fact]
    public async Task The_import_window_travels_with_the_figures()
    {
        // A number computed over "open tickets active in the last 7 days" is not the number a
        // reader assumes. The surface has to be able to say so.
        await using var db = await SeedAsync();
        db.Tickets.Add(T(Acme, "1", raised: Jun1));
        await db.SaveChangesAsync();

        var report = await new ClientWorkloadService(db).ForClientsAsync(new MetricsFilter());

        var window = report.ImportWindows.Single();
        window.ConnectionName.Should().Be("Autotask");
        window.ImportsClosedTickets.Should().BeTrue();
        window.ActiveWithinDays.Should().Be(90);
        window.TicketsHeld.Should().Be(1);
    }
}
