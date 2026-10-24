using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Model;
using Marquee.Core.Rules;

namespace Marquee.App.ViewModels;

/// <summary>Diary entries by month, one year at a time, with filters. Matches docs/design/mocks/Diary.dc.html.</summary>
public sealed partial class DiaryPageViewModel(AppServices services, Action<string> editWatch) : PageViewModel
{
    private List<int> _years = [];
    private List<Watch> _watches = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(YearText))]
    [NotifyCanExecuteChangedFor(nameof(PreviousYearCommand), nameof(NextYearCommand))]
    public partial int Year { get; set; } = DateTime.Today.Year;

    public string YearText => Year.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial string TotalText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAll), nameof(IsMovies), nameof(IsTv), nameof(IsAnime))]
    public partial SearchFilter Kind { get; set; }

    public bool IsAll => Kind == SearchFilter.All;
    public bool IsMovies => Kind == SearchFilter.Movies;
    public bool IsTv => Kind == SearchFilter.Tv;
    public bool IsAnime => Kind == SearchFilter.Anime;

    [ObservableProperty]
    public partial bool TheaterOnly { get; set; }

    [ObservableProperty]
    public partial bool RewatchesOnly { get; set; }

    [ObservableProperty]
    public partial bool WithNoteOnly { get; set; }

    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    /// <summary>Month headers and entries, in display order.</summary>
    public ObservableCollection<object> Rows { get; } = [];

    public override void OnShown()
    {
        var total = services.Watches.Count();
        TotalText = total == 1 ? "1 ENTRY ALL TIME" : $"{total:N0} ENTRIES ALL TIME";
        _years = services.Watches.Years().Append(DateTime.Today.Year).Distinct().Order().ToList();
        if (!_years.Contains(Year))
            Year = DateTime.Today.Year;
        LoadYear();
    }

    partial void OnYearChanged(int value) => LoadYear();
    partial void OnKindChanged(SearchFilter value) => Build();
    partial void OnTheaterOnlyChanged(bool value) => Build();
    partial void OnRewatchesOnlyChanged(bool value) => Build();
    partial void OnWithNoteOnlyChanged(bool value) => Build();

    [RelayCommand]
    private void SetKind(SearchFilter kind) => Kind = kind;

    [RelayCommand]
    private void ToggleTheaterOnly() => TheaterOnly = !TheaterOnly;

    [RelayCommand]
    private void ToggleRewatchesOnly() => RewatchesOnly = !RewatchesOnly;

    [RelayCommand]
    private void ToggleWithNoteOnly() => WithNoteOnly = !WithNoteOnly;

    private bool CanGoBack() => _years.Count > 0 && _years.Min() < Year;
    private bool CanGoForward() => _years.Count > 0 && _years.Max() > Year;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousYear() => Year = _years.Where(y => y < Year).Max();

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void NextYear() => Year = _years.Where(y => y > Year).Min();

    private void LoadYear()
    {
        _watches = services.Watches.ListYear(Year);
        PreviousYearCommand.NotifyCanExecuteChanged();
        NextYearCommand.NotifyCanExecuteChanged();
        Build();
    }

    private void Build()
    {
        Rows.Clear();
        var shown = _watches.Where(Matches).ToList();
        foreach (var month in shown.GroupBy(w => new DateOnly(w.WatchedOn.Year, w.WatchedOn.Month, 1)))
        {
            var count = month.Count();
            Rows.Add(new DiaryMonthHeader(
                month.Key.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                count == 1 ? "1 entry" : $"{count} entries"));
            foreach (var w in month)
            {
                var entry = new DiaryEntryViewModel(w, editWatch);
                Rows.Add(entry);
                entry.LoadPoster(services.Images);
            }
        }

        EmptyText = shown.Count > 0 ? null
            : services.Watches.Count() == 0 ? "Your diary is empty. Use Log a watch at the top, or on a search result, to add your first entry."
            : $"Nothing logged in {Year} matches these filters.";
    }

    private bool Matches(Watch w) =>
        (Kind switch
        {
            SearchFilter.Movies => w.Title?.Kind == TitleKind.Movie,
            SearchFilter.Tv => w.Title?.Kind == TitleKind.Tv,
            SearchFilter.Anime => w.Title?.Kind == TitleKind.Anime,
            _ => true,
        })
        && (!TheaterOnly || w.Where == WatchPlace.Theater)
        && (!RewatchesOnly || w.IsRewatch)
        && (!WithNoteOnly || !string.IsNullOrWhiteSpace(w.Note));
}

public sealed record DiaryMonthHeader(string Label, string Count);

/// <summary>One diary row.</summary>
public sealed partial class DiaryEntryViewModel : ViewModelBase
{
    private readonly Watch _watch;
    private readonly Action<string> _edit;

    public DiaryEntryViewModel(Watch watch, Action<string> edit)
    {
        _watch = watch;
        _edit = edit;
        var title = watch.Title!;
        Day = watch.WatchedOn.Day.ToString("00", CultureInfo.InvariantCulture);
        Name = title.Name;
        Sub = watch.Episode is { } ep ? EpisodeLabel(title, ep) : title.Year?.ToString() ?? "";
        Kind = title.Kind;
        KindLabel = LogWatchViewModel.KindLabel(title);
        Where = watch.Where switch
        {
            WatchPlace.Theater => watch.Theater?.Name ?? "Theater",
            WatchPlace.Home => "Home",
            WatchPlace.Streaming => "Streaming",
            _ => "Other",
        };
        WhereDetail = Detail(watch);
        RatingText = Rating.Format(watch.Rating);
        Note = watch.Note?.ReplaceLineEndings(" ") ?? "";
    }

    public string Day { get; }
    public string Name { get; }
    public string Sub { get; }
    public TitleKind Kind { get; }
    public string KindLabel { get; }
    public bool IsMovie => Kind == TitleKind.Movie;
    public bool IsTv => Kind == TitleKind.Tv;
    public bool IsAnime => Kind == TitleKind.Anime;
    public bool IsRewatch => _watch.IsRewatch;
    public string Where { get; }
    public string WhereDetail { get; }
    public bool HasWhereDetail => WhereDetail.Length > 0;
    public string RatingText { get; }
    public string Note { get; }

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    public async void LoadPoster(ImageLoader images) => Poster = await images.LoadSavedAsync(_watch.Title?.PosterPath, 72);

    [RelayCommand]
    private void Open() => _edit(_watch.Id);

    /// <summary>"Ep 17" for anime in its first season, "S3 E4" otherwise, "Special 2" for specials.</summary>
    public static string EpisodeLabel(Title title, Episode episode)
    {
        var season = episode.Season?.Number ?? 1;
        if (season == 0)
            return $"Special {episode.Number}";
        return title.Kind == TitleKind.Anime && season == 1 ? $"Ep {episode.Number}" : $"S{season} E{episode.Number}";
    }

    /// <summary>"Screen 4 · 35mm · with Sam" for theaters, the service for the rest.</summary>
    public static string Detail(Watch w)
    {
        var parts = new List<string>();
        if (w.Where == WatchPlace.Theater)
        {
            if (!string.IsNullOrWhiteSpace(w.Screen))
                parts.Add($"Screen {w.Screen}");
            if (!string.IsNullOrWhiteSpace(w.Format))
                parts.Add(w.Format);
        }
        else if (!string.IsNullOrWhiteSpace(w.Service))
        {
            parts.Add(w.Service);
        }

        var names = w.Companions.Select(c => c.Companion?.Name).OfType<string>().ToList();
        if (names.Count > 0)
            parts.Add("with " + string.Join(", ", names));
        else if (w.Where == WatchPlace.Theater)
            parts.Add("alone");
        return string.Join(" · ", parts);
    }
}
