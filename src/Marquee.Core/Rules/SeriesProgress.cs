using Marquee.Core.Model;

namespace Marquee.Core.Rules;

public enum SeriesState { NotStarted, Watching, Finished }

/// <summary>
/// How far along a TV show or anime series is. Specials (season 0) never count toward progress.
/// </summary>
public sealed record SeriesProgress(int Watched, int Total, int MinutesLeft, Episode? Next, SeriesState State)
{
    public double Fraction => Total == 0 ? 0 : (double)Watched / Total;

    /// <param name="seasons">The title's seasons with their episodes.</param>
    /// <param name="watchedEpisodeIds">Episodes with at least one watch.</param>
    /// <param name="defaultRuntime">Minutes per episode when an episode has no runtime of its own.</param>
    public static SeriesProgress For(IEnumerable<Season> seasons, IReadOnlySet<string> watchedEpisodeIds, int? defaultRuntime)
    {
        var main = MainEpisodes(seasons);
        var watched = main.Count(e => watchedEpisodeIds.Contains(e.Id));
        var left = main.Where(e => !watchedEpisodeIds.Contains(e.Id)).Sum(e => e.RuntimeMinutes ?? defaultRuntime ?? 0);
        var next = WatchRules.NextEpisode(main, e => watchedEpisodeIds.Contains(e.Id));
        var state = watched == 0 ? SeriesState.NotStarted
            : main.Count > 0 && watched == main.Count ? SeriesState.Finished
            : SeriesState.Watching;
        return new SeriesProgress(watched, main.Count, left, next, state);
    }

    /// <summary>The furthest main-season episode watched, for "where you stopped". Null when none.</summary>
    public static Episode? LastWatched(IEnumerable<Season> seasons, IReadOnlySet<string> watchedEpisodeIds) =>
        MainEpisodes(seasons).LastOrDefault(e => watchedEpisodeIds.Contains(e.Id));

    /// <summary>Episodes of the main seasons (not specials), in viewing order.</summary>
    public static List<Episode> MainEpisodes(IEnumerable<Season> seasons) => seasons
        .Where(s => s.Number != 0)
        .OrderBy(s => s.Number)
        .SelectMany(s => s.Episodes.OrderBy(e => e.Number))
        .ToList();

    /// <summary>"About 6 hours left", "About 1.5 hours left", "About 40 minutes left".</summary>
    public static string TimeLeftText(int minutes) => minutes switch
    {
        <= 0 => "",
        < 60 => $"About {minutes} minutes left",
        _ => $"About {FormatHours(minutes)} left",
    };

    private static string FormatHours(int minutes)
    {
        var hours = Math.Round(minutes / 60.0 * 2, MidpointRounding.AwayFromZero) / 2;
        return hours == 1 ? "1 hour" : $"{hours:0.#} hours";
    }
}
