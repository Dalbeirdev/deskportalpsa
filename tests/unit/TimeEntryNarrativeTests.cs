using Desk.PsaCore.Models;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

/// <summary>
/// The rule is deliberately narrow, and the tests exist to keep it that way: a summary is dropped
/// only when it says nothing except "look in the internal notes" AND those notes are present.
/// Every widening of this regex risks deleting something a technician actually wrote.
/// </summary>
public class TimeEntryNarrativeTests
{
    [Theory]
    [InlineData("See Internal Notes")]
    [InlineData("see internal notes")]
    [InlineData("See internal note")]
    [InlineData("Internal Notes")]
    [InlineData("Please see the internal notes.")]
    [InlineData("Refer to internal notes")]
    [InlineData("As per internal notes:")]
    [InlineData("  Check internal notes below  ")]
    public void A_summary_that_only_points_at_the_internal_notes_is_dropped(string summary)
    {
        TimeEntryNarrative.Compose(summary, "thank basit for help")
            .Should().Be("thank basit for help");
    }

    [Theory]
    [InlineData("See internal notes about the RAID controller")]
    [InlineData("Internal notes cover the rollback plan")]
    [InlineData("Replaced the switch; see internal notes")]
    [InlineData("Rebuilt the array")]
    public void A_summary_carrying_its_own_information_is_kept(string summary)
    {
        // The signpost rule must match the WHOLE summary. Text wrapped around the phrase is
        // content, and content the technician typed is not ours to delete.
        TimeEntryNarrative.Compose(summary, "detail")
            .Should().Be($"{summary}\n\ndetail");
    }

    [Fact]
    public void A_pointer_summary_with_nothing_behind_it_survives()
    {
        // Nothing to point AT: dropping this leaves a blank card, which reads as lost data rather
        // than a tidier one. The pointer is all the entry says, so the pointer is what we show.
        TimeEntryNarrative.Compose("See Internal Notes", null)
            .Should().Be("See Internal Notes");
        TimeEntryNarrative.Compose("See Internal Notes", "   ")
            .Should().Be("See Internal Notes");
    }

    [Fact]
    public void An_entry_with_only_internal_notes_shows_them()
    {
        TimeEntryNarrative.Compose(null, "internal-only detail")
            .Should().Be("internal-only detail");
    }

    [Fact]
    public void An_entry_with_no_text_at_all_composes_to_null()
    {
        TimeEntryNarrative.Compose(null, null).Should().BeNull();
        TimeEntryNarrative.Compose("  ", "\n").Should().BeNull();
    }
}
