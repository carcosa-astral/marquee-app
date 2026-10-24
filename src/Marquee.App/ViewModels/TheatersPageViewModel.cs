using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Model;
using Marquee.Core.Rules;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>The user's theaters with visits and details. Matches docs/design/mocks/Theaters.dc.html.</summary>
public sealed partial class TheatersPageViewModel(AppServices services, Action<Theater?> openForm) : PageViewModel
{
    public ObservableCollection<TheaterRowViewModel> Rows { get; } = [];
    public ObservableCollection<VisitViewModel> RecentVisits { get; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    public partial TheaterRowViewModel? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool HasNoVisits { get; set; }

    public override void OnShown()
    {
        var keep = Selected?.Id;
        Rows.Clear();
        foreach (var stats in services.Theaters.ListWithStats())
            Rows.Add(new TheaterRowViewModel(stats, Select));

        var visits = Rows.Sum(r => r.Stats.Visits);
        SummaryText = $"{Count(Rows.Count, "THEATER", "THEATERS")} · {Count(visits, "VISIT", "VISITS")}";
        IsEmpty = Rows.Count == 0;
        Select(Rows.FirstOrDefault(r => r.Id == keep) ?? Rows.FirstOrDefault());
    }

    [RelayCommand]
    private void Add() => openForm(null);

    private bool CanEdit() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit() => openForm(Selected!.Stats.Theater);

    private void Select(TheaterRowViewModel? row)
    {
        foreach (var r in Rows)
            r.IsSelected = r == row;
        Selected = row;
        RecentVisits.Clear();
        HasNoVisits = false;
        if (row is null)
            return;
        foreach (var w in services.Theaters.RecentVisits(row.Id, 5))
        {
            var visit = new VisitViewModel(w);
            RecentVisits.Add(visit);
            visit.LoadPoster(services.Images);
        }
        HasNoVisits = RecentVisits.Count == 0;
    }

    private static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n:N0} {many}";

    /// <summary>"Oct 24" this year, "Jul 18, 2025" for other years.</summary>
    public static string ShortDate(DateOnly d) =>
        d.ToString(d.Year == DateTime.Today.Year ? "MMM d" : "MMM d, yyyy", CultureInfo.InvariantCulture);
}

public sealed partial class TheaterRowViewModel(TheaterStats stats, Action<TheaterRowViewModel> select) : ViewModelBase
{
    public TheaterStats Stats { get; } = stats;
    public string Id => Stats.Theater.Id;
    public string Name => Stats.Theater.Name;
    public string Place => string.Join(" · ", new[] { Stats.Theater.City, Stats.Theater.Chain ?? "Independent" }.OfType<string>());
    public string FormatsText => Stats.Theater.Formats.Count == 0 ? "Not set" : string.Join(", ", Stats.Theater.Formats);
    public IReadOnlyList<string> Formats => Stats.Theater.Formats;
    public string Visits => Stats.Visits.ToString("N0");
    public string LastVisit => Stats.LastVisit is { } d ? TheatersPageViewModel.ShortDate(d) : "Never";
    public string Average => Rating.Format(Stats.AverageRating) is { Length: > 0 } a ? a : "-";
    public string FirstVisitYear => Stats.FirstVisit?.Year.ToString(CultureInfo.InvariantCulture) ?? "-";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Open() => select(this);
}

public sealed partial class VisitViewModel(Watch watch) : ViewModelBase
{
    public string Name { get; } = watch.Title?.Name ?? "";

    public string Detail { get; } = string.Join(" · ", new[]
    {
        TheatersPageViewModel.ShortDate(watch.WatchedOn),
        string.IsNullOrWhiteSpace(watch.Screen) ? null : $"Screen {watch.Screen}",
        watch.Format,
    }.OfType<string>());

    public string RatingText { get; } = Rating.Format(watch.Rating);

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    public async void LoadPoster(ImageLoader images) => Poster = await images.LoadSavedAsync(watch.Title?.PosterPath, 56);
}
