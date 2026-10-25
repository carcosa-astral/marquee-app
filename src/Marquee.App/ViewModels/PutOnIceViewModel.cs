using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Model;
using Marquee.Core.Rules;

namespace Marquee.App.ViewModels;

/// <summary>Pause or drop a show. Matches docs/design/mocks/PutOnIce.dc.html.</summary>
public sealed partial class PutOnIceViewModel : ViewModelBase
{
    /// <summary>Reasons offered as quick choices. You can also type your own.</summary>
    private static readonly string[] CommonReasons =
        ["Needed a break", "Lost interest", "Too slow", "Too much filler", "Waiting for a new season"];

    private readonly AppServices _services;
    private readonly string _titleId;
    private readonly Action<bool> _close;
    private readonly int? _season;
    private readonly int? _episode;

    public PutOnIceViewModel(AppServices services, string titleId, Action<bool> close)
    {
        _services = services;
        _titleId = titleId;
        _close = close;

        var title = services.Titles.GetWithEpisodes(titleId)!;
        Name = title.Name;
        var watched = services.Watches.WatchedEpisodeIds(titleId);
        var progress = SeriesProgress.For(title.Seasons, watched, title.RuntimeMinutes);
        var last = SeriesProgress.LastWatched(title.Seasons, watched);
        _season = last is null ? null : title.Seasons.First(s => s.Episodes.Any(e => e.Id == last.Id)).Number;
        _episode = last?.Number;
        var singleSeason = title.Seasons.Count(s => s.Number != 0) <= 1;
        StoppedAt = IceText.StoppedAtLong(_season, _episode, singleSeason);
        StoppedHint = $"Filled in from your diary. {progress.Watched} of {progress.Total} watched.";
        KeepsText = progress.Watched == 1
            ? "It leaves Watching and appears on the On Ice page. Your 1 watched episode stays in your diary."
            : $"It leaves Watching and appears on the On Ice page. Your {progress.Watched} watched episodes stay in your diary.";

        var existing = services.Ice.Get(titleId);
        IsDropped = existing?.State == IceState.Dropped;
        Note = existing?.Note ?? "";
        foreach (var r in CommonReasons.Concat(existing?.Reason is { } own && !CommonReasons.Contains(own) ? [own] : []))
            Reasons.Add(new ReasonChoice(r, r == existing?.Reason, PickReason));
        Date = DateTime.Today;
        _ = LoadPosterAsync(title.PosterPath);
    }

    public string Name { get; }
    public string StoppedAt { get; }
    public string StoppedHint { get; }
    public string KeepsText { get; }
    public ObservableCollection<ReasonChoice> Reasons { get; } = [];

    [ObservableProperty] public partial Bitmap? Poster { get; set; }
    [ObservableProperty] public partial DateTime? Date { get; set; }
    [ObservableProperty] public partial string Note { get; set; }
    [ObservableProperty] public partial string NewReason { get; set; } = "";
    [ObservableProperty] public partial bool IsAddingReason { get; set; }
    [ObservableProperty] public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaused))]
    public partial bool IsDropped { get; set; }

    public bool IsPaused => !IsDropped;

    private async Task LoadPosterAsync(string? path) => Poster = await _services.Images.LoadSavedAsync(path, 88);

    [RelayCommand]
    private void ChoosePaused() => IsDropped = false;

    [RelayCommand]
    private void ChooseDropped() => IsDropped = true;

    private void PickReason(ReasonChoice choice)
    {
        var select = !choice.IsSelected;
        foreach (var r in Reasons)
            r.IsSelected = select && r == choice;
    }

    [RelayCommand]
    private void StartAddingReason() => IsAddingReason = true;

    [RelayCommand]
    private void AddReason()
    {
        var text = NewReason.Trim();
        if (text.Length > 0)
        {
            var choice = Reasons.FirstOrDefault(r => string.Equals(r.Text, text, StringComparison.CurrentCultureIgnoreCase));
            if (choice is null)
            {
                choice = new ReasonChoice(text, false, PickReason);
                Reasons.Add(choice);
            }
            if (!choice.IsSelected)
                PickReason(choice);
        }
        NewReason = "";
        IsAddingReason = false;
    }

    [RelayCommand]
    private void Save()
    {
        if (!string.IsNullOrWhiteSpace(NewReason))
            AddReason();
        try
        {
            _services.Ice.Put(
                _titleId,
                IsDropped ? IceState.Dropped : IceState.Paused,
                _season,
                _episode,
                Reasons.FirstOrDefault(r => r.IsSelected)?.Text,
                Note,
                DateOnly.FromDateTime(Date ?? DateTime.Today));
            _close(true);
        }
        catch (Exception ex)
        {
            Error = "Could not save: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => _close(false);
}

public sealed partial class ReasonChoice(string text, bool selected, Action<ReasonChoice> pick) : ViewModelBase
{
    public string Text { get; } = text;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = selected;

    [RelayCommand]
    private void Pick() => pick(this);
}
