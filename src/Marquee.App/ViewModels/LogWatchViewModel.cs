using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Core.Rules;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>
/// Log a watch, or edit one from the diary. Matches docs/design/mocks/LogWatch.dc.html.
/// </summary>
public sealed partial class LogWatchViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly string? _watchId;
    private readonly Action<bool> _close;
    private readonly List<Title> _library;
    private List<Theater> _theaters;
    private bool _loading;

    /// <summary>
    /// A title picked from search that is not saved yet. It is saved, with its images,
    /// only when the watch is saved, so the library never holds titles without a watch.
    /// </summary>
    private FetchedTitle? _pending;

    /// <param name="titleId">The title to log, or null to pick one first.</param>
    /// <param name="watchId">An existing diary entry to edit, or null for a new one.</param>
    /// <param name="close">Called when the dialog closes; true when something was saved or deleted.</param>
    public LogWatchViewModel(AppServices services, string? titleId, string? watchId, Action<bool> close)
    {
        _services = services;
        _watchId = watchId;
        _close = close;
        _library = services.Titles.ListAll();
        _theaters = services.Theaters.List();
        TheaterNames = _theaters.Select(t => t.Name).ToList();
        Services = services.Watches.KnownServices();
        CompanionNames = services.Companions.List().Select(c => c.Name).ToList();
        Date = DateTime.Today;

        var existing = watchId is null ? null : services.Watches.Get(watchId);
        if (existing is not null)
            LoadExisting(existing);
        else if (titleId is not null)
            SelectTitle(titleId);
        else
            IsPickingTitle = true;
        FilterTitles();
        RefreshFormats();
    }

    /// <summary>Logs a title from search that is not in the library yet. Nothing is saved until Save.</summary>
    public LogWatchViewModel(AppServices services, FetchedTitle pending, Action<bool> close)
        : this(services, null, null, close)
    {
        _pending = pending;
        IsPickingTitle = false;
        var preview = Preview(pending);
        Title = preview;
        _ = LoadRemotePosterAsync(pending.PosterUrl);
        LoadSeasons(preview, null);
        UpdateRewatch(force: true);
    }

    /// <summary>An unsaved Title built from fetched details, so the form can show it and its episodes.</summary>
    private static Title Preview(FetchedTitle f)
    {
        var title = new Title { Name = f.Name ?? "Untitled", Year = f.Year, Kind = f.Kind, Format = f.Format };
        title.Seasons = f.Seasons.Select(fs =>
        {
            var season = new Season { TitleId = title.Id, Number = fs.Number, Name = fs.Name };
            season.Episodes = fs.Episodes.Select(fe => new Episode { SeasonId = season.Id, Number = fe.Number, Name = fe.Name }).ToList();
            return season;
        }).ToList();
        return title;
    }

    public bool IsEditing => _watchId is not null;
    public string Kicker => IsEditing ? "EDIT DIARY ENTRY" : "LOG A WATCH";
    public string SaveLabel => IsEditing ? "Save changes" : "Save to diary";

    // ----- Title -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitle), nameof(TitleName), nameof(TitleYear), nameof(ShowForm))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial Title? Title { get; set; }

    public bool HasTitle => Title is not null;
    public string TitleName => Title?.Name ?? "";
    public string TitleYear => Title?.Year?.ToString() ?? "";

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowForm))]
    public partial bool IsPickingTitle { get; set; }

    public bool ShowForm => HasTitle && !IsPickingTitle;

    [ObservableProperty]
    public partial string TitleQuery { get; set; } = "";

    public ObservableCollection<TitleChoice> TitleMatches { get; } = [];

    public bool LibraryIsEmpty => _library.Count == 0;

    partial void OnTitleQueryChanged(string value) => FilterTitles();

    [RelayCommand]
    private void ChangeTitle()
    {
        IsPickingTitle = true;
        TitleQuery = "";
    }

    [RelayCommand]
    private void PickTitle(TitleChoice choice)
    {
        SelectTitle(choice.Id);
        IsPickingTitle = false;
    }

    private void FilterTitles()
    {
        TitleMatches.Clear();
        var q = TitleQuery.Trim();
        foreach (var t in _library.Where(t => q.Length == 0 || t.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).Take(8))
            TitleMatches.Add(new TitleChoice(t.Id, t.Name, t.Year?.ToString() ?? "", KindLabel(t)));
    }

    private void SelectTitle(string titleId)
    {
        var title = _services.Titles.GetWithEpisodes(titleId);
        if (title is null)
            return;
        _pending = null;
        Title = title;
        _ = LoadPosterAsync(title.PosterPath);
        LoadSeasons(title, null);
        UpdateRewatch(force: true);
    }

    private async Task LoadPosterAsync(string? path) => Poster = await _services.Images.LoadSavedAsync(path, 88);

    private async Task LoadRemotePosterAsync(string? url) => Poster = await _services.Images.LoadRemoteAsync(url, 88);

    // ----- Episodes -----

    public ObservableCollection<SeasonChoice> Seasons { get; } = [];
    public ObservableCollection<EpisodeChoice> Episodes { get; } = [];

    /// <summary>
    /// True for TV shows and anime series. Their episodes are always watched at home,
    /// and the rating is for the whole show rather than the episode.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWhere), nameof(ShowsService), nameof(RatingLabel), nameof(RatingTip))]
    public partial bool HasEpisodes { get; set; }

    public bool ShowsWhere => !HasEpisodes;
    public string RatingLabel => HasEpisodes ? "Your rating for the show" : "Rating";
    public string RatingTip => HasEpisodes
        ? "Rate the whole show. Episodes are not rated one by one."
        : "Click the left half of a star for a half star. Click again to clear.";

    [ObservableProperty]
    public partial SeasonChoice? SelectedSeason { get; set; }

    [ObservableProperty]
    public partial EpisodeChoice? SelectedEpisode { get; set; }

    partial void OnSelectedSeasonChanged(SeasonChoice? value)
    {
        Episodes.Clear();
        Episodes.Add(EpisodeChoice.None);
        foreach (var e in value?.Episodes ?? [])
            Episodes.Add(new EpisodeChoice(e.Id, value!.Number, e.Number, $"Episode {e.Number}" + (string.IsNullOrWhiteSpace(e.Name) || e.Name == $"Episode {e.Number}" ? "" : $" · {e.Name}")));
        if (!_loading)
            SelectedEpisode = Episodes.Count > 1 ? Episodes[1] : Episodes[0];
    }

    partial void OnSelectedEpisodeChanged(EpisodeChoice? value) => UpdateRewatch(force: false);

    private void LoadSeasons(Title title, string? episodeId)
    {
        _loading = true;
        Seasons.Clear();
        // Specials (season 0) go last so "next episode" follows the main seasons.
        foreach (var s in title.Seasons.OrderBy(s => s.Number == 0 ? int.MaxValue : s.Number))
            Seasons.Add(new SeasonChoice(s.Number, SeasonLabel(s, title), s.Episodes));
        HasEpisodes = Seasons.Any(s => s.Episodes.Count > 0);
        if (HasEpisodes)
        {
            Where = WatchPlace.Home;
            Rating = title.UserRating;
        }

        Episode? target = null;
        if (episodeId is not null)
            target = title.Seasons.SelectMany(s => s.Episodes).FirstOrDefault(e => e.Id == episodeId);
        else if (HasEpisodes)
        {
            var watched = _services.Watches.WatchedEpisodeIds(title.Id);
            var main = Seasons.Where(s => s.Number != 0).SelectMany(s => s.Episodes).ToList();
            target = WatchRules.NextEpisode(main, e => watched.Contains(e.Id));
        }

        SelectedSeason = target is null ? Seasons.FirstOrDefault() : Seasons.First(s => s.Episodes.Contains(target));
        _loading = false;
        SelectedEpisode = target is null ? EpisodeChoice.None : Episodes.FirstOrDefault(e => e.Id == target.Id) ?? EpisodeChoice.None;
    }

    private static string SeasonLabel(Season s, Title title) =>
        s.Number == 0 ? "Specials"
        : title.Seasons.Count(x => x.Number != 0) == 1 && title.Kind == TitleKind.Anime ? "Episodes"
        : $"Season {s.Number}";

    // ----- Date and time -----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial string StartTime { get; set; } = "";

    [ObservableProperty]
    public partial string? TimeError { get; set; }

    partial void OnStartTimeChanged(string value) =>
        TimeError = TimeInput.TryParse(value, out _) ? null : "Use a time like 7:30 PM.";

    [RelayCommand]
    private void Today() => Date = DateTime.Today;

    [RelayCommand]
    private void Yesterday() => Date = DateTime.Today.AddDays(-1);

    // ----- Rating and rewatch -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RatingText), nameof(HasRating))]
    public partial double? Rating { get; set; }

    public bool HasRating => Rating is not null;
    public string RatingText => Core.Rules.Rating.Format(Rating);

    [ObservableProperty]
    public partial bool IsRewatch { get; set; }

    [ObservableProperty]
    public partial string RewatchHint { get; set; } = "First time watching";

    private void UpdateRewatch(bool force)
    {
        if (Title is null || _loading)
            return;
        var first = _services.Watches.FirstSeen(Title.Id, SelectedEpisode?.Id, _watchId);
        RewatchHint = first is null ? "First time watching" : $"First seen {first.Value.ToString("MMM yyyy", CultureInfo.InvariantCulture)}";
        if (!IsEditing || force)
            IsRewatch = first is not null;
    }

    // ----- Where -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTheater), nameof(IsHome), nameof(IsOther), nameof(ShowsService))]
    public partial WatchPlace Where { get; set; } = WatchPlace.Home;

    public bool IsTheater => Where == WatchPlace.Theater;
    public bool IsHome => Where == WatchPlace.Home;
    public bool IsOther => Where == WatchPlace.Other;
    public bool ShowsService => !IsTheater || HasEpisodes;

    [RelayCommand]
    private void SetWhere(WatchPlace place) => Where = place;

    // ----- Theater details -----

    public List<string> TheaterNames { get; private set; }

    [ObservableProperty]
    public partial string? TheaterName { get; set; }

    [ObservableProperty]
    public partial string Screen { get; set; } = "";

    [ObservableProperty]
    public partial string Seat { get; set; } = "";

    [ObservableProperty]
    public partial string? Format { get; set; }

    public ObservableCollection<FormatOption> FormatOptions { get; } = [];

    [ObservableProperty]
    public partial TheaterFormViewModel? TheaterForm { get; set; }

    partial void OnTheaterNameChanged(string? value) => RefreshFormats();

    partial void OnFormatChanged(string? value)
    {
        foreach (var f in FormatOptions)
            f.IsSelected = string.Equals(f.Name, value, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void SelectFormat(string format) => Format = string.Equals(Format, format, StringComparison.OrdinalIgnoreCase) ? null : format;

    [RelayCommand]
    private void AddTheater()
    {
        var typed = TheaterName?.Trim();
        var isNew = !string.IsNullOrEmpty(typed) && FindTheater(typed) is null;
        TheaterForm = new TheaterFormViewModel(_services.Theaters, null, OnTheaterFormDone, isNew ? typed : null);
    }

    private void OnTheaterFormDone(Theater? saved)
    {
        TheaterForm = null;
        if (saved is null)
            return;
        _theaters = _services.Theaters.List();
        TheaterNames = _theaters.Select(t => t.Name).ToList();
        OnPropertyChanged(nameof(TheaterNames));
        TheaterName = saved.Name;
        RefreshFormats();
    }

    private Theater? FindTheater(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null
        : _theaters.FirstOrDefault(t => string.Equals(t.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));

    private void RefreshFormats()
    {
        var offered = FindTheater(TheaterName)?.Formats is { Count: > 0 } f ? f : TheaterStore.CommonFormats.ToList();
        var names = offered.ToList();
        if (Format is not null && !names.Contains(Format, StringComparer.OrdinalIgnoreCase))
            names.Add(Format);
        FormatOptions.Clear();
        foreach (var name in names)
            FormatOptions.Add(new FormatOption(name, string.Equals(name, Format, StringComparison.OrdinalIgnoreCase), SelectFormat));
    }

    // ----- Service -----

    public List<string> Services { get; }

    [ObservableProperty]
    public partial string? Service { get; set; }

    // ----- Companions -----

    public List<string> CompanionNames { get; }

    public ObservableCollection<CompanionChip> Companions { get; } = [];

    [ObservableProperty]
    public partial string? CompanionQuery { get; set; }

    [ObservableProperty]
    public partial bool IsAddingCompanion { get; set; }

    [RelayCommand]
    private void StartAddingCompanion() => IsAddingCompanion = true;

    [RelayCommand]
    private void AddCompanion()
    {
        var name = CompanionQuery?.Trim();
        if (!string.IsNullOrEmpty(name) && Companions.All(c => !string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            Companions.Add(new CompanionChip(name, RemoveCompanion));
        CompanionQuery = "";
        IsAddingCompanion = false;
    }

    private void RemoveCompanion(CompanionChip chip) => Companions.Remove(chip);

    // ----- Note -----

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    // ----- Save, cancel, delete -----

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteLabel))]
    public partial bool IsConfirmingDelete { get; set; }

    public string DeleteLabel => IsConfirmingDelete ? "Click again to delete" : "Delete entry";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; set; }

    public string SaveText => IsSaving ? "Saving..." : SaveLabel;

    private bool CanSave() => Title is not null && Date is not null && !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (!TimeInput.TryParse(StartTime, out var time))
        {
            Error = TimeError;
            return;
        }
        IsSaving = true;
        try
        {
            var (titleId, episodeId) = await SaveTitleIfPendingAsync();

            // A name typed for something not yet in the lists is added to them.
            if (!string.IsNullOrWhiteSpace(CompanionQuery))
                AddCompanion();
            var theater = IsTheater && !string.IsNullOrWhiteSpace(TheaterName) ? _services.Theaters.FindOrCreate(TheaterName) : null;
            var companionIds = Companions.Select(c => _services.Companions.FindOrCreate(c.Name).Id).ToList();

            var input = new WatchInput
            {
                TitleId = titleId,
                EpisodeId = episodeId,
                WatchedOn = DateOnly.FromDateTime(Date!.Value),
                StartTime = time,
                Rating = HasEpisodes ? null : Rating,
                IsRewatch = IsRewatch,
                Where = HasEpisodes ? WatchPlace.Home : Where,
                TheaterId = theater?.Id,
                Screen = Screen,
                Seat = Seat,
                Format = Format,
                Service = Service,
                Note = Note,
                CompanionIds = companionIds,
            };
            if (_watchId is null)
                _services.Watches.Add(input);
            else
                _services.Watches.Update(_watchId, input);
            if (HasEpisodes)
                _services.Titles.SetUserRating(titleId, Rating);
            _close(true);
        }
        catch (Exception ex)
        {
            Error = "Could not save: " + ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>Saves a title picked from search (with its images) and finds the chosen episode in it.</summary>
    private async Task<(string TitleId, string? EpisodeId)> SaveTitleIfPendingAsync()
    {
        if (_pending is null)
            return (Title!.Id, SelectedEpisode?.Id);

        var pending = _pending;
        var images = await _services.Downloader.SaveAllAsync(pending);
        var saved = await Task.Run(() => _services.Titles.Save(pending, images));
        _pending = null;
        await _services.ApplyImdbRatingsAsync(saved.Id);
        var episodeId = SelectedEpisode is { Season: { } season, Number: { } number }
            ? _services.Titles.FindEpisodeId(saved.Id, season, number)
            : null;
        return (saved.Id, episodeId);
    }

    [RelayCommand]
    private void Cancel() => _close(false);

    [RelayCommand]
    private void Delete()
    {
        if (_watchId is null)
            return;
        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            return;
        }
        _services.Watches.Delete(_watchId);
        _close(true);
    }

    private void LoadExisting(Watch w)
    {
        _loading = true;
        var title = _services.Titles.GetWithEpisodes(w.TitleId)!;
        Title = title;
        _ = LoadPosterAsync(title.PosterPath);
        _loading = false;
        LoadSeasons(title, w.EpisodeId);

        Date = w.WatchedOn.ToDateTime(TimeOnly.MinValue);
        StartTime = TimeInput.Format(w.StartTime);
        Rating = HasEpisodes ? title.UserRating : w.Rating;
        Where = HasEpisodes ? WatchPlace.Home : w.Where;
        TheaterName = w.Theater?.Name;
        Screen = w.Screen ?? "";
        Seat = w.Seat ?? "";
        Format = w.Format;
        Service = w.Service;
        Note = w.Note ?? "";
        foreach (var c in w.Companions.Where(c => c.Companion is not null))
            Companions.Add(new CompanionChip(c.Companion!.Name, RemoveCompanion));
        UpdateRewatch(force: false);
        IsRewatch = w.IsRewatch;
        RefreshFormats();
    }

    internal static string KindLabel(Title t) => t.Kind switch
    {
        TitleKind.Movie => "Movie",
        TitleKind.Tv => "TV",
        _ => "Anime",
    };
}

public sealed record TitleChoice(string Id, string Name, string Year, string Kind);

public sealed record SeasonChoice(int Number, string Label, List<Episode> Episodes);

/// <summary>An episode to pick. Season and number find it again once a new title is saved.</summary>
public sealed record EpisodeChoice(string? Id, int? Season, int? Number, string Label)
{
    public static readonly EpisodeChoice None = new(null, null, null, "No specific episode");
}

public sealed partial class FormatOption(string name, bool selected, Action<string> select) : ViewModelBase
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = selected;

    [RelayCommand]
    private void Select() => select(Name);
}

public sealed partial class CompanionChip(string name, Action<CompanionChip> remove) : ViewModelBase
{
    public string Name { get; } = name;

    [RelayCommand]
    private void Remove() => remove(this);
}
