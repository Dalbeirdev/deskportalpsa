using Desk.Api.Controllers;
using Desk.Application.Analytics;
using Desk.Application.Common;
using Desk.Domain.Authorization;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The technician dashboard took its technician filter straight from the query string, so anyone
/// holding the own-productivity permission could read a named colleague's figures by passing their
/// id — or the whole organization's by passing none, since an absent filter means "no restriction"
/// downstream. A Technician holds exactly that permission, so this was reachable by every
/// technician, with no id guessing: just an edited URL.
///
/// These tests pin the clamp that fixes it, by capturing the filter the controller actually hands
/// to the metrics service.
/// </summary>
public class ProductivityScopeTests
{
    private sealed class CapturingMetrics : ITechnicianMetricsService
    {
        public MetricsFilter? Captured { get; private set; }

        public Task<TechnicianMetrics> ForTechnicianAsync(MetricsFilter f, ProductivityWeights w, CancellationToken ct = default)
        {
            Captured = f;
            return Task.FromResult(new TechnicianMetrics { TechnicianExternalId = f.TechnicianExternalId ?? "(all)" });
        }

        public Task<IReadOnlyList<TeamComparisonRow>> TeamAsync(MetricsFilter f, ProductivityWeights w, CancellationToken ct = default)
        {
            Captured = f;
            return Task.FromResult<IReadOnlyList<TeamComparisonRow>>([]);
        }

        public Task<IReadOnlyList<TrendPoint>> TrendAsync(MetricsFilter f, CancellationToken ct = default)
        {
            Captured = f;
            return Task.FromResult<IReadOnlyList<TrendPoint>>([]);
        }
    }

    private static DashboardController Controller(CapturingMetrics metrics, ICurrentUserStub user)
        => new(metrics, new UnusedClientWorkload(), user);

    /// <summary>These tests are about technician scoping; the client surface is not exercised.</summary>
    private sealed class UnusedClientWorkload : Desk.Application.Analytics.IClientWorkloadService
    {
        public Task<Desk.Application.Analytics.ClientWorkloadReport> ForClientsAsync(
            Desk.Application.Analytics.MetricsFilter filter, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    /// <summary>Minimal ICurrentUser we can point at a specific permission set + technician id.</summary>
    private sealed class ICurrentUserStub(string? tech, params string[] perms) : Desk.Application.Abstractions.ICurrentUser
    {
        public bool IsAuthenticated => true;
        public string? Subject => "sub";
        public string? Email => "t@test";
        public string? DisplayName => "Tech";
        public Guid? OrganizationId => Guid.NewGuid();
        public Guid? UserId => Guid.NewGuid();
        public string? TechnicianExternalId => tech;
        public IReadOnlySet<string> Permissions => perms.ToHashSet();
        public bool HasPermission(string permissionKey) => perms.Contains(permissionKey);
    }

    [Fact]
    public async Task A_technician_asking_for_a_colleague_is_pinned_to_themselves()
    {
        var metrics = new CapturingMetrics();
        var user = new ICurrentUserStub("tech-me", Permissions.ProductivityViewOwn);

        await Controller(metrics, user).Technician(
            new DashboardController.DashboardQuery { Technician = "tech-someone-else" }, default);

        metrics.Captured!.TechnicianExternalId.Should().Be("tech-me");
    }

    [Fact]
    public async Task A_technician_asking_for_everyone_is_pinned_to_themselves()
    {
        // The dangerous case: omitting the filter entirely used to mean "no restriction",
        // returning org-wide figures.
        var metrics = new CapturingMetrics();
        var user = new ICurrentUserStub("tech-me", Permissions.ProductivityViewOwn);

        await Controller(metrics, user).Technician(new DashboardController.DashboardQuery(), default);

        metrics.Captured!.TechnicianExternalId.Should().Be("tech-me");
    }

    [Fact]
    public async Task A_manager_with_the_team_permission_may_still_ask_for_a_named_technician()
    {
        var metrics = new CapturingMetrics();
        var user = new ICurrentUserStub("mgr", Permissions.ProductivityViewOwn, Permissions.ProductivityViewTeam);

        await Controller(metrics, user).Technician(
            new DashboardController.DashboardQuery { Technician = "tech-someone-else" }, default);

        metrics.Captured!.TechnicianExternalId.Should().Be("tech-someone-else");
    }

    [Fact]
    public async Task An_unlinked_account_is_refused_rather_than_shown_everything()
    {
        // Fail closed: with no technician id there is nothing to clamp to, and leaving the filter
        // null would hand back the whole organization — the exact leak being fixed.
        var metrics = new CapturingMetrics();
        var user = new ICurrentUserStub(null, Permissions.ProductivityViewOwn);

        var act = () => Controller(metrics, user).Technician(new DashboardController.DashboardQuery(), default);

        await act.Should().ThrowAsync<ForbiddenException>();
        metrics.Captured.Should().BeNull("the service must not be reached at all");
    }
}
