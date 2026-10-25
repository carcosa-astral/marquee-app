using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Model;
using Marquee.Core.Rules;

namespace Marquee.App.ViewModels;

/// <summary>Shows you paused or dropped. Matches docs/design/mocks/OnIce.dc.html.</summary>
public sealed partial class OnIcePageViewModel(AppServices services, Action<string> openTitle, Action changed) : PageViewModel
{
    private List<IceEntry> _all = [];

    public ObservableCollection<IceEntry> Items { get; } = [];

    [ObservableProperty] public partial string CountText { get; set; } = "";
    [ObservableProperty] public partial string AllLabel { get; set; } = "All";
    [ObservableProperty] public partial string PausedLabel { get; set; } = "Paused";
    [ObservableProperty] public partial string DroppedLabel { get; set; } = "Dropped";
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    /// <summary>Null shows everything; otherwise only paused or only dropped.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAll), nameof(IsPaused), nameof(IsDropped))]
    public partial IceState? Filter { get; set; }

    public bool IsAll => Filter is null;
    public bool IsPaused => Filter == IceState.Paused;
    public bool IsDropped => Filter == IceState.Dropped;

    public override void OnShown()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = services.Ice.List().Where(i => i.Title is not null).ToList();
        _all = items.Select(i =>
        {
            var watched = services.Watches.WatchedEpisodeIds(i.TitleId);
            return new IceEntry(i, SeriesProgress.For(i.Title!.Seasons, watched, i.Title.RuntimeMinutes), today, openTitle, Resume, Toggle);
        }).ToList();
        var paused = _all.Count(e => e.State == IceState.Paused);
        CountText = _all.Count == 1 ? "1 SHOW SET ASIDE" : $"{_all.Count} SHOWS SET ASIDE";
        AllLabel = $"All {_all.Count}";
        PausedLabel = $"Paused {paused}";
        DroppedLabel = $"Dropped {_all.Count - paused}";
        IsEmpty = _all.Count == 0;
        ApplyFilter();
    }

    partial void OnFilterChanged(IceState? value) => ApplyFilter();

    [RelayCommand]
    private void ShowAll() => Filter = null;

    [RelayCommand]
    private void ShowPaused() => Filter = IceState.Paused;

    [RelayCommand]
    private void ShowDropped() => Filter = IceState.Dropped;

    private void ApplyFilter()
    {
        Items.Clear();
        foreach (var e in _all.Where(e => Filter is null || e.State == Filter))
        {
            Items.Add(e);
            e.LoadPoster(services.Images);
        }
    }

    private void Resume(IceEntry e)
    {
        services.Ice.Resume(e.TitleId);
        OnShown();
        changed();
    }

    private void Toggle(IceEntry e)
    {
        services.Ice.SetState(e.TitleId, e.State == IceState.Paused ? IceState.Dropped : IceState.Paused);
        OnShown();
        changed();
    }
}

public sealed partial class IceEntry : ViewModelBase
{
    private readonly Action<string> _open;
    private readonly Action<IceEntry> _resume;
    private readonly Action<IceEntry> _toggle;
    private readonly string? _posterPath;

    public IceEntry(IceItem item, SeriesProgress progress, DateOnly today, Action<string> open, Action<IceEntry> resume, Action<IceEntry> toggle)
    {
        _open = open;
        _resume = resume;
        _toggle = toggle;
        var t = item.Title!;
        _posterPath = t.PosterPath;
        TitleId = t.Id;
        Name = t.Name;
        Kind = t.Kind;
        KindLabel = t.Kind == TitleKind.Tv ? "TV" : t.Kind == TitleKind.Anime ? "Anime" : "Movie";
        State = item.State;
        StoppedAt = IceText.StoppedAt(t, item.StoppedAtSeason, item.StoppedAtEpisode, progress.Total);
        Percent = progress.Total == 0 ? 0 : progress.Fraction * 100;
        PercentText = progress.Total == 0 ? "" : $"{Math.Round(Percent):0}%";
        Reason = string.Join(" ", new[] { Sentence(item.Reason), item.Note }.OfType<string>());
        IcedText = $"Iced {item.IcedOn.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)} · {WatchlistRules.AgoText(item.IcedOn, today)}";
    }

    public string TitleId { get; }
    public string Name { get; }
    public TitleKind Kind { get; }
    public bool IsTv => Kind == TitleKind.Tv;
    public bool IsAnime => Kind == TitleKind.Anime;
    public string KindLabel { get; }
    public IceState State { get; }
    public bool IsPaused => State == IceState.Paused;
    public bool IsDropped => State == IceState.Dropped;
    public string StateLabel => IsPaused ? "Paused" : "Dropped";
    public string ToggleLabel => IsPaused ? "Move to dropped" : "Move to paused";
    public string StoppedAt { get; }
    public double Percent { get; }
    public string PercentText { get; }
    public string Reason { get; }
    public bool HasReason => Reason.Length > 0;
    public string IcedText { get; }

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    public async void LoadPoster(ImageLoader images) => Poster ??= await images.LoadSavedAsync(_posterPath, 200);

    /// <summary>"Needed a break" becomes "Needed a break." so a note can follow it.</summary>
    private static string? Sentence(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.TrimEnd() is var t && ".!?".Contains(t[^1]) ? t : t + ".";

    [RelayCommand]
    private void Open() => _open(TitleId);

    [RelayCommand]
    private void Resume() => _resume(this);

    [RelayCommand]
    private void Toggle() => _toggle(this);
}

/// <summary>How "where you stopped" reads.</summary>
public static class IceText
{
    /// <summary>"S2 E3" for TV, "Ep 64 of 366" for single-season anime, "the start" when nothing was watched.</summary>
    public static string StoppedAt(Title t, int? season, int? episode, int totalEpisodes)
    {
        if (episode is null)
            return "the start";
        var mainSeasons = t.Seasons.Count(s => s.Number != 0);
        return t.Kind == TitleKind.Anime && mainSeasons <= 1
            ? $"Ep {episode} of {totalEpisodes}"
            : $"S{season ?? 1} E{episode}";
    }

    /// <summary>"Season 1, after episode 17" for the dialog.</summary>
    public static string StoppedAtLong(int? season, int? episode, bool singleSeason) =>
        episode is null ? "Before the first episode"
        : singleSeason ? $"After episode {episode}"
        : $"Season {season}, after episode {episode}";
}
