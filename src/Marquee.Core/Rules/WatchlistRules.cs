namespace Marquee.Core.Rules;

/// <summary>
/// Coming soon or out now (build plan, section 6). Worked out from today's date each time,
/// so titles move to "Out now" on their release day with no action from the user.
/// </summary>
public static class WatchlistRules
{
    /// <summary>TMDB and AniList statuses that mean "not out yet".</summary>
    private static readonly HashSet<string> UnreleasedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rumored", "Planned", "In Production", "Post Production", "Not yet released", "Upcoming",
    };

    /// <summary>The date that decides coming soon or out now: the title's own date, else the one the user typed.</summary>
    public static DateOnly? ReleaseDate(DateOnly? titleRelease, DateOnly? expected) => titleRelease ?? expected;

    /// <summary>
    /// Coming soon when the release date is after today, or when there is no date yet
    /// and the source says the title is not out.
    /// </summary>
    public static bool IsComingSoon(DateOnly? releaseDate, string? status, DateOnly today) =>
        releaseDate is { } d ? d > today : status is not null && UnreleasedStatuses.Contains(status.Trim());

    /// <summary>"Today", "Tomorrow", "in 21 days", or "Date unconfirmed".</summary>
    public static string CountdownText(DateOnly? releaseDate, DateOnly today)
    {
        if (releaseDate is not { } d)
            return "Date unconfirmed";
        var days = d.DayNumber - today.DayNumber;
        return days switch
        {
            <= 0 => "Today",
            1 => "Tomorrow",
            _ => $"in {days} days",
        };
    }

    /// <summary>"71 days ago", "Yesterday", "Today".</summary>
    public static string AgoText(DateOnly date, DateOnly today)
    {
        var days = today.DayNumber - date.DayNumber;
        return days switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            _ => $"{days} days ago",
        };
    }
}
