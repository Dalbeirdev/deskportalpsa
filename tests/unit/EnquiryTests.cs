using Desk.Application.Marketing;
using Desk.Domain.Authorization;
using Desk.Domain.Enums;
using Desk.Domain.Marketing;
using Desk.Infrastructure.Marketing;
using Desk.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The public forms are the only unauthenticated write in the product, so what they accept and
/// refuse is a security boundary, not a validation nicety.
/// </summary>
public class EnquiryTests
{
    private static (EnquiryService Svc, DeskDbContext Db) Build()
    {
        var h = AdminHarness.Create(Guid.NewGuid());
        return (new EnquiryService(h.Db, h.Clock), h.Db);
    }

    private static SubmitEnquiryInput Input(
        string name = "Dana Reed", string email = "dana@acme.test", string message = "Can you sync Halo?",
        string? website = null) =>
        new(EnquiryKind.Contact, name, email, "Acme", "555", message, null, "/contact", website);

    [Fact]
    public async Task A_valid_enquiry_is_stored()
    {
        var (svc, db) = Build();
        await using var _ = db;

        (await svc.SubmitAsync(Input())).Should().BeTrue();

        var row = await db.Enquiries.SingleAsync();
        row.Name.Should().Be("Dana Reed");
        row.Status.Should().Be(EnquiryStatus.New);
        row.SourcePage.Should().Be("/contact");
    }

    [Theory]
    [InlineData("", "dana@acme.test", "hello")]
    [InlineData("Dana", "", "hello")]
    [InlineData("Dana", "dana@acme.test", "")]
    [InlineData("Dana", "not-an-email", "hello")]
    [InlineData("Dana", "dana@localhost", "hello")]  // no dot in host: unreachable in practice
    public async Task Unusable_submissions_are_refused_and_store_nothing(string name, string email, string message)
    {
        var (svc, db) = Build();
        await using var _ = db;

        (await svc.SubmitAsync(Input(name, email, message))).Should().BeFalse();
        (await db.Enquiries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_tripped_honeypot_looks_like_success_but_stores_nothing()
    {
        var (svc, db) = Build();
        await using var _ = db;

        // Reporting the block would tell a bot exactly which field to stop filling in.
        (await svc.SubmitAsync(Input(website: "http://spam.example"))).Should().BeTrue();
        (await db.Enquiries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Oversized_fields_are_clipped_rather_than_bursting_the_column()
    {
        var (svc, db) = Build();
        await using var _ = db;

        await svc.SubmitAsync(Input(message: new string('x', 9_000)));

        (await db.Enquiries.SingleAsync()).Message.Length.Should().Be(4_000);
    }

    [Fact]
    public async Task The_list_reports_how_many_are_still_unanswered()
    {
        var (svc, db) = Build();
        await using var _ = db;
        await svc.SubmitAsync(Input(name: "One"));
        await svc.SubmitAsync(Input(name: "Two"));

        var first = (await svc.ListAsync()).Items.First();
        (await svc.SetStatusAsync(first.Id, EnquiryStatus.Closed)).Should().BeTrue();

        var after = await svc.ListAsync();
        after.Total.Should().Be(2);
        after.NewCount.Should().Be(1);
        (await svc.ListAsync(EnquiryStatus.New)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task An_enquiry_arrives_before_any_tenant_exists_so_it_is_not_tenant_scoped()
    {
        // A tenant filter here would either invent an organization or hide every row from the
        // admin who needs to answer it. This asserts the row is readable with no tenant scope.
        var (svc, db) = Build();
        await using var _ = db;
        await svc.SubmitAsync(Input());

        db.Model.FindEntityType(typeof(Enquiry))!.GetQueryFilter().Should().BeNull();
        (await db.Enquiries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Administrators_and_managers_can_see_enquiries_but_technicians_cannot()
    {
        // The claim has to actually be granted, or the page is unreachable for everyone.
        Permissions.ForRole(RoleType.MspAdministrator).Should().Contain(Permissions.EnquiriesView);
        Permissions.ForRole(RoleType.Manager).Should().Contain(Permissions.EnquiriesView);
        Permissions.ForRole(RoleType.Technician).Should().NotContain(Permissions.EnquiriesView);
        Permissions.ForRole(RoleType.ClientUser).Should().NotContain(Permissions.EnquiriesView);
        Permissions.All.Should().Contain(Permissions.EnquiriesView);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_role_seeded_before_a_permission_existed_gains_it_on_the_next_start()
    {
        // The live symptom this prevents: ship a new claim, deploy, and the feature is invisible
        // because the deployed role rows predate it and the seeder used to skip them.
        // Each phase gets its own context over one database: the scenario is a deployment
        // restarting against storage that already holds roles from an older build.
        var dbName = Guid.NewGuid().ToString();
        var org = Guid.NewGuid();

        await using (var first = AdminHarness.Create(org, dbName).Db)
            await DatabaseSeeder.SeedBuiltInRolesAsync(first);

        await using (var older = AdminHarness.Create(org, dbName).Db)
        {
            var admin = await older.Roles.IgnoreQueryFilters().Include(r => r.Permissions)
                .SingleAsync(r => r.BuiltInType == RoleType.MspAdministrator);
            foreach (var stale in admin.Permissions.Where(p => p.PermissionKey == Permissions.EnquiriesView).ToList())
                admin.Permissions.Remove(stale);
            await older.SaveChangesAsync();
        }

        await using (var restarted = AdminHarness.Create(org, dbName).Db)
            await DatabaseSeeder.SeedBuiltInRolesAsync(restarted);

        await using var check = AdminHarness.Create(org, dbName).Db;
        var after = await check.Roles.IgnoreQueryFilters().Include(r => r.Permissions)
            .SingleAsync(r => r.BuiltInType == RoleType.MspAdministrator);
        after.Permissions.Select(p => p.PermissionKey).Should().Contain(Permissions.EnquiriesView);
    }
}
