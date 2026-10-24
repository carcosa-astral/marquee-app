using System.Globalization;

namespace Marquee.Core.Rules;

/// <summary>Ratings run from 0.5 to 5 in half steps.</summary>
public static class Rating
{
    public const double Min = 0.5;
    public const double Max = 5;

    public static bool IsValid(double? value) =>
        value is null || (value >= Min && value <= Max && value * 2 == Math.Round(value.Value * 2));

    /// <summary>Snaps to the nearest half star inside the allowed range.</summary>
    public static double Snap(double value) => Math.Clamp(Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2, Min, Max);

    public static string Format(double? value) => value?.ToString("0.0", CultureInfo.InvariantCulture) ?? "";
}

/// <summary>Reads the optional start time typed into the log dialog.</summary>
public static class TimeInput
{
    private static readonly string[] Formats =
    [
        "h:mm tt", "h:mmtt", "h tt", "htt", "hh:mm tt", "hh:mmtt",
        "H:mm", "HH:mm", "H.mm", "HHmm",
    ];

    /// <summary>Accepts "7:30 PM", "7:30pm", "7pm" and "19:30". Blank means no time.</summary>
    public static bool TryParse(string? text, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        var cleaned = text.Trim().ToUpperInvariant().Replace("A.M.", "AM").Replace("P.M.", "PM");
        if (TimeOnly.TryParseExact(cleaned, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
        {
            time = t;
            return true;
        }
        return false;
    }

    public static string Format(TimeOnly? time) => time?.ToString("h:mm tt", CultureInfo.InvariantCulture) ?? "";
}

public static class WatchRules
{
    /// <summary>
    /// The episode to suggest next: the one after the furthest episode watched, or the first
    /// episode when nothing has been watched. Null when everything has been watched.
    /// </summary>
    public static T? NextEpisode<T>(IReadOnlyList<T> episodesInOrder, Func<T, bool> isWatched) where T : class
    {
        if (episodesInOrder.Count == 0)
            return null;
        var last = -1;
        for (var i = 0; i < episodesInOrder.Count; i++)
        {
            if (isWatched(episodesInOrder[i]))
                last = i;
        }
        return last + 1 < episodesInOrder.Count ? episodesInOrder[last + 1] : null;
    }
}
