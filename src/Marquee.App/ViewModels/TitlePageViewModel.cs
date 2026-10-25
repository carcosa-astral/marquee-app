using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Core.Rules;
using Marquee.Data;
using Marquee.Metadata;

namespace Marquee.App.ViewModels;

/// <summary>What the title page needs from the shell.</summary>
public sealed record TitlePageActions(
    Action<string?, string?> OpenLog,
    Action<ViewModelBase?> OpenDialog,
    Action Back,
    Action Changed);

/// <summary>
/// The page for one movie, TV show or anime. Matches docs/design/mocks/Movie, Show and TvShow.
/// </summary>
public sealed partial class TitlePageViewModel : PageViewModel
{
    private readonly AppServices _services;
    private readonly string _titleId;
    private readonly TitlePageActions _actions;
    private Title _title = new();
    private List<Watch> _watches = [];
    private HashSet<string> _watchedEpisodes = [];
    private SeriesProgress? _progress;

    public TitlePageViewModel(AppServices services, string titleId, TitlePageActions actions)
    {
        _services = services;
        _titleId = titleId;
        _actions = actions;
    }

    public string TitleId => _titleId;

    // ----- Header -----

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string YearText { get; set; } = "";
    [ObservableProperty] public partial string KindLabel { get; set; } = "";
    [ObservableProperty] public partial string StatusLabel { get; set; } = "";
    [ObservableProperty] public partial bool StatusIsActive { get; set; }
    [ObservableProperty] public partial string MetaLine { get; set; } = "";
    [ObservableProperty] public partial string? DirectedBy { get; set; }
    [ObservableProperty] public partial string? Overview { get; set; }

    /// <summary>Scores from IMDb ("8.3"), TMDB ("7.4") and AniList ("86%"), when known.</summary>
    [ObservableProperty] public partial string? ImdbRating { get; set; }
    [ObservableProperty] public partial string? TmdbScore { get; set; }
    [ObservableProperty] public partial string? AniListScore { get; set; }
    [ObservableProperty] public partial bool HasScores { get; set; }
    [ObservableProperty] public partial bool IsMovie { get; set; }
    [ObservableProperty] public partial bool IsTv { get; set; }
    [ObservableProperty] public partial bool IsAnime { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsColumn), nameof(DetailsSpan))]
    public partial bool IsSeries { get; set; }

    /// <summary>Movies have no cast list beside the details, so the Details card spans both columns.</summary>
    public int DetailsColumn => IsSeries ? 1 : 0;
    public int DetailsSpan => IsSeries ? 1 : 2;
    [ObservableProperty] public partial Bitmap? Poster { get; set; }
    [ObservableProperty] public partial Bitmap? Backdrop { get; set; }
    [ObservableProperty] public partial string? BackdropSource { get; set; }
    [ObservableProperty] public partial string? YourRating { get; set; }
    [ObservableProperty] public partial string PrimaryActionLabel { get; set; } = "Log a watch";
    [ObservableProperty] public partial string? Message { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    // ----- Data sources -----

    [ObservableProperty] public partial bool FromTmdb { get; set; }
    [ObservableProperty] public partial bool FromAniList { get; set; }
    [ObservableProperty] public partial string EditedText { get; set; } = "";

    public ObservableCollection<DetailSourceRow> Details { get; } = [];

    // ----- Movies -----

    public ObservableCollection<TitleWatchRow> Watches { get; } = [];
    public ObservableCollection<TitleWatchRow> WhereSeen { get; } = [];
    [ObservableProperty] public partial bool HasWatches { get; set; }

    // ----- Series -----

    [ObservableProperty] public partial string ProgressText { get; set; } = "";
    [ObservableProperty] public partial string TimeLeftText { get; set; } = "";
    [ObservableProperty] public partial double ProgressPercent { get; set; }
    public ObservableCollection<SeasonTab> SeasonTabs { get; } = [];
    [ObservableProperty] public partial bool HasSeasonTabs { get; set; }
    [ObservableProperty] public partial SeasonTab? SelectedSeason { get; set; }
    [ObservableProperty] public partial string SeasonHeading { get; set; } = "";
    [ObservableProperty] public partial string SeasonProgressText { get; set; } = "";
    public ObservableCollection<EpisodeRow> EpisodeGrid { get; } = [];
    public ObservableCollection<EpisodeRow> EpisodeList { get; } = [];
    [ObservableProperty] public partial bool ShowsGrid { get; set; }
    [ObservableProperty] public partial bool HasEpisodes { get; set; }
    /// <summary>Your rating for the whole show. Changing it here saves it right away.</summary>
    [ObservableProperty] public partial double? ShowRating { get; set; }
    private bool _settingShowRating;

    partial void OnShowRatingChanged(double? value)
    {
        if (_settingShowRating)
            return;
        _services.Titles.SetUserRating(_titleId, value);
        _actions.Changed();
    }

    /// <summary>One main season: "Mark through episode". Several: "Mark season watched".</summary>
    [ObservableProperty] public partial bool UsesMarkThrough { get; set; }
    [ObservableProperty] public partial bool IsMarkingThrough { get; set; }
    [ObservableProperty] public partial string MarkThroughText { get; set; } = "";
    [ObservableProperty] public partial bool IsConfirmingSeason { get; set; }
    public string MarkSeasonLabel => IsConfirmingSeason ? "Click again to mark" : "Mark season watched";

    partial void OnIsConfirmingSeasonChanged(bool value) => OnPropertyChanged(nameof(MarkSeasonLabel));

    // ----- People -----

    public ObservableCollection<CastMember> Cast { get; } = [];
    [ObservableProperty] public partial bool HasCast { get; set; }

    public override void OnShown() => Load();

    private void Load()
    {
        var details = _services.Titles.GetDetails(_titleId);
        if (details is null)
        {
            _actions.Back();
            return;
        }
        _title = details.Title;
        _watches = _services.Watches.ListForTitle(_titleId);
        _watchedEpisodes = _watches.Where(w => w.EpisodeId is not null).Select(w => w.EpisodeId!).ToHashSet();

        var t = _title;
        IsAnime = t.Kind == TitleKind.Anime;
        IsTv = t.Kind == TitleKind.Tv;
        IsSeries = SeriesProgress.MainEpisodes(t.Seasons).Count > 0;
        IsMovie = !IsSeries;
        Name = t.Name;
        YearText = t.Year?.ToString(CultureInfo.InvariantCulture) ?? "";
        KindLabel = t.Kind switch
        {
            TitleKind.Movie => "Movie",
            TitleKind.Tv => "TV",
            _ => "Anime · " + t.Format switch
            {
                TitleFormat.Film or TitleFormat.Movie => "Film",
                TitleFormat.Ova => "OVA",
                TitleFormat.Ona => "ONA",
                TitleFormat.Special => "Special",
                _ => "Series",
            },
        };
        Overview = t.Overview;
        ImdbRating = t.ImdbRating?.ToString("0.0", CultureInfo.InvariantCulture);
        TmdbScore = t.TmdbScore?.ToString("0.0", CultureInfo.InvariantCulture);
        AniListScore = t.AniListScore is { } al ? $"{al}%" : null;
        HasScores = ImdbRating is not null || TmdbScore is not null || AniListScore is not null;
        var directors = t.Credits.Where(c => c.Role == CreditRole.Director).Select(c => c.Person?.Name).OfType<string>().Distinct().Take(3).ToList();
        DirectedBy = directors.Count == 0 ? null : string.Join(", ", directors);

        FromTmdb = t.TmdbId is not null;
        FromAniList = t.AniListId is not null;
        EditedText = details.Edits.Count == 1 ? "1 field edited" : $"{details.Edits.Count} fields edited";
        BackdropSource = details.Sources.TryGetValue(TitleFields.Backdrop, out var bs) ? $"Backdrop from {SourceName(bs)}" : null;

        _ = LoadImagesAsync(t);
        BuildDetails(details);
        BuildWatches();
        BuildSeries();
        BuildCast();
    }

    private async Task LoadImagesAsync(Title t)
    {
        Poster = await _services.Images.LoadSavedAsync(t.PosterPath, 420);
        Backdrop = await _services.Images.LoadSavedAsync(t.BackdropPath, 1400);
    }

    private void BuildDetails(TitleDetails d)
    {
        var t = d.Title;
        Details.Clear();
        void Row(string label, string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            d.Sources.TryGetValue(field, out var source);
            Details.Add(new DetailSourceRow(label, value, d.Sources.ContainsKey(field) ? source : null));
        }
        Row("Original title", t.OriginalName, TitleFields.OriginalName);
        Row("Released", t.ReleaseDate?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) ?? t.Year?.ToString(),
            t.ReleaseDate is null ? TitleFields.Year : TitleFields.ReleaseDate);
        Row("Runtime", t.RuntimeMinutes is > 0 ? $"{t.RuntimeMinutes} min" : null, TitleFields.Runtime);
        Row("Status", t.Status, TitleFields.Status);
        Row("Poster", t.PosterPath is null ? null : "Image", TitleFields.Poster);
        Row("Backdrop", t.BackdropPath is null ? null : "Image", TitleFields.Backdrop);

        var genres = t.Genres.Select(g => g.Genre?.Name).OfType<string>().Take(3).ToList();
        var parts = new List<string>();
        var main = SeriesProgress.MainEpisodes(t.Seasons);
        if (main.Count > 0)
            parts.Add(main.Count == 1 ? "1 episode" : $"{main.Count} episodes");
        if (t.RuntimeMinutes is > 0)
            parts.Add($"{t.RuntimeMinutes} min");
        if (genres.Count > 0)
            parts.Add(string.Join(", ", genres));
        if (IsSeries && t.Seasons.Count(s => s.Number != 0) > 1)
            parts.Insert(0, $"{t.Seasons.Count(s => s.Number != 0)} seasons");
        MetaLine = string.Join(" · ", parts);
    }

    private void BuildWatches()
    {
        Watches.Clear();
        WhereSeen.Clear();
        var titleWatches = _watches.Where(w => w.EpisodeId is null).ToList();
        var first = titleWatches.MinBy(w => w.WatchedOn);
        foreach (var w in titleWatches)
            Watches.Add(new TitleWatchRow(w, w == first, OpenWatch));
        foreach (var w in titleWatches.Take(3))
            WhereSeen.Add(new TitleWatchRow(w, w == first, OpenWatch));
        HasWatches = Watches.Count > 0;

        YourRating = Rating.Format(titleWatches.FirstOrDefault(w => w.Rating is not null)?.Rating);
        if (IsMovie)
        {
            StatusLabel = titleWatches.Count switch { 0 => "Not watched yet", 1 => "Watched once", var n => $"Watched {n} times" };
            StatusIsActive = false;
            PrimaryActionLabel = "Log a watch";
        }
    }

    private void BuildSeries()
    {
        SeasonTabs.Clear();
        EpisodeGrid.Clear();
        EpisodeList.Clear();
        if (!IsSeries)
            return;

        _settingShowRating = true;
        ShowRating = _title.UserRating;
        _settingShowRating = false;

        _progress = SeriesProgress.For(_title.Seasons, _watchedEpisodes, _title.RuntimeMinutes);
        var p = _progress;
        StatusLabel = p.State switch { SeriesState.Finished => "Finished", SeriesState.Watching => "Watching", _ => "Not started" };
        StatusIsActive = p.State == SeriesState.Watching;
        ProgressText = $"{p.Watched} of {p.Total} episodes";
        TimeLeftText = SeriesProgress.TimeLeftText(p.MinutesLeft);
        ProgressPercent = p.Fraction * 100;

        var mainSeasons = _title.Seasons.Where(s => s.Number != 0).ToList();
        UsesMarkThrough = mainSeasons.Count == 1;
        var nextSeason = _title.Seasons.FirstOrDefault(s => p.Next is not null && s.Episodes.Any(e => e.Id == p.Next.Id));
        PrimaryActionLabel = p.Next is null ? "Log a watch"
            : mainSeasons.Count > 1 ? $"Log episode {p.Next.Number} of season {nextSeason?.Number}"
            : $"Log episode {p.Next.Number}";

        // Specials go last.
        foreach (var s in _title.Seasons.OrderBy(s => s.Number == 0 ? int.MaxValue : s.Number))
        {
            var watched = s.Episodes.Count(e => _watchedEpisodes.Contains(e.Id));
            var label = s.Number == 0 ? "Specials" : $"Season {s.Number}";
            var sub = watched == 0 ? "Not started" : $"{watched} of {s.Episodes.Count}";
            SeasonTabs.Add(new SeasonTab(s.Number, label, sub, watched == s.Episodes.Count && watched > 0, SelectSeason));
        }
        HasSeasonTabs = SeasonTabs.Count > 1;

        var keep = SelectedSeason?.Number ?? nextSeason?.Number ?? SeasonTabs.FirstOrDefault()?.Number;
        SelectSeason(SeasonTabs.FirstOrDefault(s => s.Number == keep) ?? SeasonTabs.FirstOrDefault());
    }

    private void SelectSeason(SeasonTab? tab)
    {
        foreach (var t in SeasonTabs)
            t.IsSelected = t == tab;
        SelectedSeason = tab;
        EpisodeGrid.Clear();
        EpisodeList.Clear();
        IsConfirmingSeason = false;
        var season = tab is null ? null : _title.Seasons.FirstOrDefault(s => s.Number == tab.Number);
        if (season is null)
        {
            HasEpisodes = false;
            return;
        }

        var watched = season.Episodes.Count(e => _watchedEpisodes.Contains(e.Id));
        SeasonHeading = tab!.Label;
        SeasonProgressText = $"{watched} of {season.Episodes.Count} episodes watched";
        var rows = season.Episodes.Select(e => new EpisodeRow(
            e,
            _watches.Where(w => w.EpisodeId == e.Id).OrderByDescending(w => w.WatchedOn).FirstOrDefault(),
            _progress?.Next?.Id == e.Id,
            e.RuntimeMinutes ?? _title.RuntimeMinutes,
            ToggleEpisode)).ToList();

        // Long anime seasons get the compact grid plus a few rows around the next episode.
        ShowsGrid = IsAnime && rows.Count > 13;
        if (ShowsGrid)
        {
            foreach (var r in rows)
                EpisodeGrid.Add(r);
            var center = Math.Max(0, rows.FindIndex(r => r.IsNext) is var i && i >= 0 ? i : rows.FindLastIndex(r => r.IsWatched));
            foreach (var r in rows.Skip(Math.Max(0, center - 2)).Take(4))
                EpisodeList.Add(r);
        }
        else
        {
            foreach (var r in rows)
                EpisodeList.Add(r);
        }
        HasEpisodes = rows.Count > 0;
    }

    private void BuildCast()
    {
        Cast.Clear();
        var credits = _title.Credits.Where(c => c.Person is not null).ToList();
        var people = IsMovie
            ? credits.Where(c => c.Role != CreditRole.Director).Take(8).ToList()
            : credits.Where(c => c.Role != CreditRole.Director).Take(5).Concat(credits.Where(c => c.Role == CreditRole.Director).Take(1)).ToList();
        var counts = _services.Titles.WatchedTitleCounts(people.Select(c => c.PersonId));
        foreach (var c in people.DistinctBy(c => c.PersonId))
        {
            var role = c.Role switch
            {
                CreditRole.Director => "Director",
                CreditRole.Voice => string.IsNullOrWhiteSpace(c.Character) ? "Voice" : $"{c.Character} (voice)",
                _ => c.Character ?? "",
            };
            var member = new CastMember(c.Person!.Name, role, counts.GetValueOrDefault(c.PersonId));
            Cast.Add(member);
            member.LoadPhoto(_services.Images, c.Person.PhotoPath);
        }
        HasCast = Cast.Count > 0;
    }

    // ----- Actions -----

    [RelayCommand]
    private void Back() => _actions.Back();

    [RelayCommand]
    private void LogWatch() => _actions.OpenLog(_titleId, null);

    private void OpenWatch(string watchId) => _actions.OpenLog(null, watchId);

    /// <summary>An unwatched episode is marked watched today. A watched one opens its diary entry.</summary>
    private void ToggleEpisode(EpisodeRow row)
    {
        if (row.LatestWatchId is { } watchId)
        {
            OpenWatch(watchId);
            return;
        }
        _services.Watches.MarkEpisodesWatched(_titleId, [row.EpisodeId], DateOnly.FromDateTime(DateTime.Today));
        AfterChange($"Marked episode {row.Number} watched today.");
    }

    [RelayCommand]
    private void StartMarkThrough()
    {
        IsMarkingThrough = true;
        MarkThroughText = (_progress?.Next?.Number ?? 1).ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private void CancelMarkThrough() => IsMarkingThrough = false;

    [RelayCommand]
    private void ConfirmMarkThrough()
    {
        if (!int.TryParse(MarkThroughText, out var through) || through < 1)
        {
            Message = "Type an episode number, like 12.";
            return;
        }
        var ids = SeriesProgress.MainEpisodes(_title.Seasons).Where(e => e.Number <= through).Select(e => e.Id);
        var added = _services.Watches.MarkEpisodesWatched(_titleId, ids, DateOnly.FromDateTime(DateTime.Today));
        IsMarkingThrough = false;
        AfterChange(added == 0 ? "Those episodes were already watched." : $"Marked {added} episodes watched today.");
    }

    [RelayCommand]
    private void MarkSeason()
    {
        if (!IsConfirmingSeason)
        {
            IsConfirmingSeason = true;
            return;
        }
        var season = _title.Seasons.FirstOrDefault(s => s.Number == SelectedSeason?.Number);
        if (season is null)
            return;
        var added = _services.Watches.MarkEpisodesWatched(_titleId, season.Episodes.Select(e => e.Id), DateOnly.FromDateTime(DateTime.Today));
        AfterChange(added == 0 ? "That season was already watched." : $"Marked {added} episodes watched today.");
    }

    [RelayCommand]
    private void EditDetails() => _actions.OpenDialog(new EditDetailsViewModel(_services, _titleId, Closed));

    [RelayCommand]
    private void FixMatch() => _actions.OpenDialog(new FixMatchViewModel(_services, _titleId, Closed));

    [RelayCommand]
    private async Task Refresh()
    {
        IsBusy = true;
        Message = "Refreshing details...";
        try
        {
            var fetched = await _services.Metadata.RefreshAsync(_title.TmdbType, _title.TmdbId, _title.AniListId);
            var images = await _services.Downloader.SaveAllAsync(fetched);
            await Task.Run(() => _services.Titles.Save(fetched, images));
            await _services.ApplyImdbRatingsAsync(_titleId);
            AfterChange("Details refreshed. Your edits and diary entries were kept.");
        }
        catch (MetadataException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = "Could not refresh: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Closed(bool changed)
    {
        _actions.OpenDialog(null);
        if (changed)
            AfterChange(null);
    }

    private void AfterChange(string? message)
    {
        Load();
        Message = message;
        _actions.Changed();
    }

    internal static string SourceName(MetadataSource s) => s switch
    {
        MetadataSource.Tmdb => "TMDB",
        MetadataSource.AniList => "AniList",
        _ => "You",
    };
}

/// <summary>One row of the Details card: a value and where it came from.</summary>
public sealed record DetailSourceRow(string Label, string Value, MetadataSource? Source)
{
    public string SourceText => Source switch
    {
        MetadataSource.Tmdb => "TMDB",
        MetadataSource.AniList => "AniList",
        MetadataSource.User => "You edited",
        _ => "",
    };
    public bool IsTmdb => Source == MetadataSource.Tmdb;
    public bool IsAniList => Source == MetadataSource.AniList;
    public bool IsUser => Source == MetadataSource.User;
}

/// <summary>A diary entry shown on a movie's page.</summary>
public sealed partial class TitleWatchRow(Watch watch, bool isFirst, Action<string> open) : ViewModelBase
{
    public string Date { get; } = watch.WatchedOn.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    public string Kind { get; } = watch.IsRewatch || !isFirst ? "Rewatch" : "First watch";
    public bool IsRewatch => Kind == "Rewatch";
    public string Where { get; } = watch.Where switch
    {
        WatchPlace.Theater => string.Join(", ", new[] { watch.Theater?.Name ?? "Theater", watch.Theater?.City }.OfType<string>()),
        WatchPlace.Home => "Home",
        _ => "Other",
    };
    public string Detail { get; } = DiaryEntryViewModel.Detail(watch);
    public string ShortDetail { get; } = string.Join(" · ", new[]
    {
        watch.WatchedOn.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
        watch.Where == WatchPlace.Theater && !string.IsNullOrWhiteSpace(watch.Screen) ? $"Screen {watch.Screen}" : null,
        watch.Where == WatchPlace.Theater ? watch.Format : watch.Service,
    }.Where(p => !string.IsNullOrWhiteSpace(p)));
    public string? Note { get; } = watch.Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public string RatingText { get; } = Rating.Format(watch.Rating);

    [RelayCommand]
    private void Open() => open(watch.Id);
}

public sealed partial class SeasonTab(int number, string label, string progress, bool complete, Action<SeasonTab> select) : ViewModelBase
{
    public int Number { get; } = number;
    public string Label { get; } = label;
    public string Progress { get; } = progress;
    public bool IsComplete { get; } = complete;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => select(this);
}

public sealed partial class EpisodeRow : ViewModelBase
{
    private readonly Action<EpisodeRow> _toggle;

    public EpisodeRow(Episode episode, Watch? latest, bool isNext, int? runtime, Action<EpisodeRow> toggle)
    {
        _toggle = toggle;
        EpisodeId = episode.Id;
        Number = episode.Number;
        Name = string.IsNullOrWhiteSpace(episode.Name) ? $"Episode {episode.Number}" : episode.Name;
        RuntimeText = runtime is > 0 ? $"{runtime} min" : "";
        LatestWatchId = latest?.Id;
        IsWatched = latest is not null;
        IsNext = isNext && !IsWatched;
        StatusText = latest is not null ? $"Watched {TheatersPageViewModel.ShortDate(latest.WatchedOn)}"
            : IsNext ? "Up next"
            : "Not watched";
    }

    public string EpisodeId { get; }
    public int Number { get; }
    public string Name { get; }
    public string RuntimeText { get; }
    public string? LatestWatchId { get; }
    public bool IsWatched { get; }
    public bool IsNext { get; }
    public bool IsPlain => !IsWatched && !IsNext;
    public string StatusText { get; }
    public string Tip => IsWatched ? "Open this diary entry" : "Mark watched today";

    [RelayCommand]
    private void Toggle() => _toggle(this);
}

public sealed partial class CastMember(string name, string role, int watched) : ViewModelBase
{
    public string Name { get; } = name;
    public string Role { get; } = role;
    public string Initials { get; } = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0]));
    public string WatchedText { get; } = watched == 0 ? "Not seen" : watched == 1 ? "1 watched" : $"{watched} watched";
    public bool HasWatched { get; } = watched > 0;

    [ObservableProperty]
    public partial Bitmap? Photo { get; set; }

    public async void LoadPhoto(ImageLoader images, string? path) => Photo = await images.LoadSavedAsync(path, 160);
}
