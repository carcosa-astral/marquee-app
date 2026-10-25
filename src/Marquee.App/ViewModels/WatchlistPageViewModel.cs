using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Model;
using Marquee.Core.Rules;

namespace Marquee.App.ViewModels;

/// <summary>Coming soon and out now. Matches docs/design/mocks/Watchlist.dc.html.</summary>
public sealed partial class WatchlistPageViewModel(AppServices services, Action<string> openTitle, Action changed) : PageViewModel
{
    private List<WatchlistEntry> _outNow = [];

    public ObservableCollection<WatchlistEntry> ComingSoon { get; } = [];
    public ObservableCollection<WatchlistEntry> OutNow { get; } = [];

    [ObservableProperty] public partial string CountText { get; set; } = "";
    [ObservableProperty] public partial string ComingSoonSummary { get; set; } = "";
    [ObservableProperty] public partial bool HasComingSoon { get; set; }
    [ObservableProperty] public partial bool HasOutNow { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    [ObservableProperty] public partial string? OutNowEmptyText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAll), nameof(IsMovies), nameof(IsTv), nameof(IsAnime), nameof(IsShort))]
    public partial WatchlistFilter Filter { get; set; }

    public bool IsAll => Filter == WatchlistFilter.All;
    public bool IsMovies => Filter == WatchlistFilter.Movies;
    public bool IsTv => Filter == WatchlistFilter.Tv;
    public bool IsAnime => Filter == WatchlistFilter.Anime;
    public bool IsShort => Filter == WatchlistFilter.UnderTwoHours;

    public override void OnShown()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = services.Watchlist.List().Where(i => i.Title is not null).ToList();
        CountText = items.Count == 1 ? "1 TITLE" : $"{items.Count} TITLES";
        IsEmpty = items.Count == 0;

        ComingSoon.Clear();
        var soon = items
            .Select(i => (Item: i, Date: WatchlistRules.ReleaseDate(i.Title!.ReleaseDate, i.ExpectedRelease)))
            .Where(x => WatchlistRules.IsComingSoon(x.Date, x.Item.Title!.Status, today))
            .OrderBy(x => x.Date ?? DateOnly.MaxValue)
            .ToList();
        foreach (var (item, date) in soon)
        {
            var e = new WatchlistEntry(item, date, today, comingSoon: true, openTitle, Remove);
            ComingSoon.Add(e);
            e.LoadPoster(services.Images);
        }
        HasComingSoon = ComingSoon.Count > 0;
        var dated = soon.Count(x => x.Date is not null);
        var undated = soon.Count - dated;
        ComingSoonSummary = (dated, undated) switch
        {
            (_, 0) => $"{Plural(dated, "title")} with a release date. These move to \"Out now\" on their own.",
            (0, _) => $"{Plural(undated, "title")} without a confirmed date.",
            _ => $"{dated} with a release date, {undated} unconfirmed. These move to \"Out now\" on their own.",
        };

        var soonIds = soon.Select(x => x.Item.Id).ToHashSet();
        _outNow = items.Where(i => !soonIds.Contains(i.Id))
            .Select(i => new WatchlistEntry(i, WatchlistRules.ReleaseDate(i.Title!.ReleaseDate, i.ExpectedRelease), today, comingSoon: false, openTitle, Remove))
            .ToList();
        ApplyFilter();
    }

    partial void OnFilterChanged(WatchlistFilter value) => ApplyFilter();

    [RelayCommand]
    private void SetFilter(WatchlistFilter filter) => Filter = filter;

    /// <summary>Opens a random title from "Out now" (using the current filter).</summary>
    [RelayCommand]
    private void PickForMe()
    {
        if (OutNow.Count == 0)
            return;
        openTitle(OutNow[Random.Shared.Next(OutNow.Count)].TitleId);
    }

    private void ApplyFilter()
    {
        OutNow.Clear();
        foreach (var e in _outNow.Where(Matches))
        {
            OutNow.Add(e);
            e.LoadPoster(services.Images);
        }
        HasOutNow = OutNow.Count > 0;
        OutNowEmptyText = _outNow.Count == 0 ? "Nothing here yet. Add titles from search or from a title's page."
            : OutNow.Count == 0 ? "Nothing matches this filter."
            : null;
    }

    private bool Matches(WatchlistEntry e) => Filter switch
    {
        WatchlistFilter.Movies => e.Kind == TitleKind.Movie,
        WatchlistFilter.Tv => e.Kind == TitleKind.Tv,
        WatchlistFilter.Anime => e.Kind == TitleKind.Anime,
        WatchlistFilter.UnderTwoHours => e.IsFilm && e.Runtime is > 0 and < 120,
        _ => true,
    };

    private void Remove(WatchlistEntry e)
    {
        services.Watchlist.Remove(e.TitleId);
        OnShown();
        changed();
    }

    private static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";
}

public enum WatchlistFilter { All, Movies, Tv, Anime, UnderTwoHours }

public sealed partial class WatchlistEntry : ViewModelBase
{
    private readonly Action<string> _open;
    private readonly Action<WatchlistEntry> _remove;
    private readonly string? _posterPath;

    public WatchlistEntry(WatchlistItem item, DateOnly? release, DateOnly today, bool comingSoon, Action<string> open, Action<WatchlistEntry> remove)
    {
        _open = open;
        _remove = remove;
        var t = item.Title!;
        _posterPath = t.PosterPath;
        TitleId = t.Id;
        Name = t.Name;
        Kind = t.Kind;
        Runtime = t.RuntimeMinutes;
        var episodes = SeriesProgress.MainEpisodes(t.Seasons).Count;
        IsFilm = episodes == 0;
        KindLabel = SearchResultViewModel.Label(t.Kind, t.Format);
        Month = release?.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant() ?? "TBA";
        Day = release?.Day.ToString(CultureInfo.InvariantCulture) ?? "?";
        Countdown = WatchlistRules.CountdownText(release, today);
        HasDate = release is not null;
        Added = $"Added {TheatersPageViewModel.ShortDate(item.AddedOn)}";

        var seasons = t.Seasons.Count(s => s.Number != 0);
        var size = IsFilm
            ? (t.RuntimeMinutes is > 0 ? $"{t.RuntimeMinutes} min" : null)
            : t.Kind == TitleKind.Anime && seasons <= 1 ? $"{episodes} eps" : seasons == 1 ? "1 season" : $"{seasons} seasons";
        Meta = comingSoon
            ? (release is { } d ? $"Releases {d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}"
                : t.Year is { } y ? $"Expected {y}, exact date not announced"
                : "Release date not announced")
            : string.Join(" · ", new[] { t.Year?.ToString(CultureInfo.InvariantCulture), KindLabel, size }.OfType<string>());
    }

    public string TitleId { get; }
    public string Name { get; }
    public TitleKind Kind { get; }
    public bool IsMovie => Kind == TitleKind.Movie;
    public bool IsTv => Kind == TitleKind.Tv;
    public bool IsAnime => Kind == TitleKind.Anime;
    public bool IsFilm { get; }
    public int? Runtime { get; }
    public string KindLabel { get; }
    public string Month { get; }
    public string Day { get; }
    public string Countdown { get; }
    public bool HasDate { get; }
    public string Meta { get; }
    public string Added { get; }

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    public async void LoadPoster(ImageLoader images) => Poster ??= await images.LoadSavedAsync(_posterPath, 300);

    [RelayCommand]
    private void Open() => _open(TitleId);

    [RelayCommand]
    private void Remove() => _remove(this);
}
