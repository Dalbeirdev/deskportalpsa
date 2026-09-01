using System.Text.RegularExpressions;

namespace Desk.PsaCore.Models;

/// <summary>
/// Turns a time entry's two text halves into the one body a reader sees.
///
/// Providers split them: Autotask keeps a summary and a separate internal-notes field (its UI
/// labels the second "Internal Only"). Technicians routinely use the summary as a signpost —
/// "See Internal Notes" — and write the substance in the other half. Joining both blindly puts
/// the signpost directly above the destination, which is noise; showing only the summary was
/// worse still, a pointer to something never displayed.
/// </summary>
public static partial class TimeEntryNarrative
{
    /// <summary>
    /// A summary that says nothing except "the real text is in the internal notes". Deliberately
    /// narrow: it must match the WHOLE summary, so "See internal notes about the RAID controller"
    /// is left alone — that one carries information of its own. Anchored, case-insensitive, and
    /// tolerant of trailing punctuation and the usual politeness.
    /// </summary>
    [GeneratedRegex(
        @"^(please\s+|pls\s+|kindly\s+)?((see|refer(\s+to)?|as\s+per|check|view|read)\s+)?(the\s+)?internal\s+note(s)?(\s+(below|above))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PointerOnly();

    /// <summary>
    /// The text to show for an entry, or null when it has none at all.
    /// A pointer-only summary is dropped ONLY when the notes it points at are actually present —
    /// with nothing behind it, "See Internal Notes" is still the only thing the entry says, and
    /// deleting it would leave a blank card that reads as data loss rather than tidiness.
    /// </summary>
    public static string? Compose(string? summary, string? internalNotes)
    {
        var s = summary?.Trim();
        var i = internalNotes?.Trim();
        var hasInternal = !string.IsNullOrWhiteSpace(i);

        if (hasInternal && !string.IsNullOrWhiteSpace(s) && IsPointerOnly(s)) s = null;

        var body = string.Join("\n\n",
            new[] { s, i }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private static bool IsPointerOnly(string summary) =>
        PointerOnly().IsMatch(summary.Trim().TrimEnd('.', ':', '-', '—', '!', ' '));
}
