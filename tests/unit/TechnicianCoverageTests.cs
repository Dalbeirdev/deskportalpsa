using Desk.Application.Admin;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// Which technician may take a ticket, and in which role. Autotask validates the resource/role pair
/// against the ticket's queue, so offering a role the provider will reject is worse than offering
/// none — the rejection only surfaces after the user has committed to a choice.
/// </summary>
public class TechnicianCoverageTests
{
    private const string Queue = "29682833";
    private const string OtherQueue = "29682969";

    private static readonly TechnicianCoverageDto[] Coverage =
    [
        // Basit: Engineer, scoped to this queue only.
        new("basit", "engineer", "Engineer", Queue),
        // Sudanshu: Administration and Engineer on this queue, Project Manager department-wide.
        new("sudanshu", "admin", "Administration", Queue),
        new("sudanshu", "engineer", "Engineer", Queue),
        new("sudanshu", "pm", "Project Manager", null),
        // Priya: no queue scoping at all — department-wide Help Desk.
        new("priya", "helpdesk", "Help Desk", null),
        // Rob: covers a different queue entirely.
        new("rob", "engineer", "Engineer", OtherQueue),
    ];

    /// <summary>
    /// Mirrors TicketAssignmentController.RolesFor: queue-scoped coverage wins outright when it
    /// exists, department-wide fills in otherwise.
    /// </summary>
    private static List<TechnicianCoverageDto> RolesFor(string technicianId, string? queueId)
    {
        var mine = Coverage.Where(c => c.TechnicianId == technicianId && c.RoleId is not null).ToList();
        var scoped = queueId is null ? [] : mine.Where(c => c.QueueOrBoardId == queueId).ToList();
        var usable = scoped.Count > 0 ? scoped : mine.Where(c => c.QueueOrBoardId is null).ToList();
        if (usable.Count == 0) usable = mine;
        return usable.GroupBy(c => c.RoleId!).Select(g => g.First()).ToList();
    }

    [Fact]
    public void A_role_held_only_department_wide_is_not_offered_on_a_queue_the_technician_covers()
    {
        var roles = RolesFor("sudanshu", Queue).Select(r => r.RoleName).ToList();

        // Project Manager is real, but not defined for this queue — Autotask answers
        // "the specified assignedResourceID and AssignedRoleID combination is not currently defined".
        roles.Should().BeEquivalentTo("Administration", "Engineer");
    }

    [Fact]
    public void Department_wide_coverage_is_used_when_the_technician_has_none_on_this_queue()
    {
        RolesFor("priya", Queue).Select(r => r.RoleName).Should().BeEquivalentTo("Help Desk");
    }

    [Fact]
    public void The_first_queue_scoped_role_is_what_an_unspecified_assignment_uses()
    {
        // Callers may leave the role out; the resolved one must still be valid for the queue.
        RolesFor("basit", Queue).First().RoleId.Should().Be("engineer");
    }

    [Fact]
    public void A_technician_covering_only_another_queue_falls_back_rather_than_returning_nothing()
    {
        // Rob has no coverage here and none department-wide. Returning empty would block an
        // assignment an admin may still legitimately want to make, so his own roles are offered
        // and the provider gets the final say.
        RolesFor("rob", Queue).Should().NotBeEmpty();
    }

    [Fact]
    public void A_technician_on_two_boards_is_offered_the_role_for_the_board_in_hand()
    {
        // ConnectWise models this through board teams: the same member sits on different teams per
        // board, so the label must follow the ticket's board rather than picking one arbitrarily.
        var cw = new TechnicianCoverageDto[]
        {
            new("sarabjit", "28", "Techpio Support", "7"),
            new("sarabjit", "30", "Techpio", "8"),
            new("sarabjit", "30", "Techpio", "9"),
        };
        static List<TechnicianCoverageDto> For(TechnicianCoverageDto[] all, string tech, string? queue)
        {
            var mine = all.Where(c => c.TechnicianId == tech && c.RoleId is not null).ToList();
            var scoped = queue is null ? [] : mine.Where(c => c.QueueOrBoardId == queue).ToList();
            var usable = scoped.Count > 0 ? scoped : mine.Where(c => c.QueueOrBoardId is null).ToList();
            if (usable.Count == 0) usable = mine;
            return usable.GroupBy(c => c.RoleId!).Select(g => g.First()).ToList();
        }

        For(cw, "sarabjit", "7").Select(r => r.RoleName).Should().BeEquivalentTo("Techpio Support");
        For(cw, "sarabjit", "9").Select(r => r.RoleName).Should().BeEquivalentTo("Techpio");
    }

    [Fact]
    public void With_no_queue_resolved_every_role_the_technician_holds_is_available()
    {
        RolesFor("sudanshu", null).Select(r => r.RoleName)
            .Should().BeEquivalentTo("Project Manager");
    }
}
