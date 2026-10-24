using Marquee.Core.Storage;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>
/// A simple page for the 0.1.0 skeleton: a heading, a short description and
/// an optional card. Real pages replace these milestone by milestone.
/// </summary>
public sealed class PageViewModel : ViewModelBase
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? CardTitle { get; init; }
    public string? CardText { get; init; }
    public IReadOnlyList<DetailRow> Details { get; init; } = [];
    public string? Footnote { get; init; }

    public bool HasCard => CardTitle is not null;
    public bool HasDetails => Details.Count > 0;
    public bool HasFootnote => Footnote is not null;
}

public sealed record DetailRow(string Label, string Value);

public static class Pages
{
    public static PageViewModel Home() => new()
    {
        Title = "Home",
        Description = "Continue watching, what is in progress, and your year so far.",
        CardTitle = "Your diary is empty",
        CardText = "Nothing is preloaded. Searching for titles arrives in 0.2.0, and logging watches in 0.3.0.",
    };

    public static PageViewModel Soon(string title, string description, string milestone) => new()
    {
        Title = title,
        Description = description,
        CardTitle = $"Coming in {milestone}",
        CardText = "This page is part of a later milestone in the build plan.",
    };

    public static PageViewModel Settings(MarqueeFolder folder, SettingsStore settings) => new()
    {
        Title = "Settings",
        Description = "Your Marquee folder. Backups, metadata, appearance and import arrive in 0.8.0.",
        Details =
        [
            new("Marquee folder", folder.Root),
            new("Diary", folder.Database),
            new("Images", folder.Images),
            new("Backups", folder.Backups),
            new("Exports", folder.Exports),
            new("TMDB API key", string.IsNullOrEmpty(settings.Get(SettingsStore.TmdbApiKey)) ? "Not set" : "Saved"),
            new("Version", typeof(Pages).Assembly.GetName().Version?.ToString(3) ?? ""),
        ],
        Footnote = "This product uses the TMDB API but is not endorsed or certified by TMDB. Anime data from AniList.",
    };
}
