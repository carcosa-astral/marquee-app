using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>The main window: sidebar, top bar and the current page.</summary>
public partial class ShellViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly SearchPageViewModel _search;

    public ShellViewModel(AppServices services)
    {
        _services = services;
        _search = new SearchPageViewModel(services, titleId => OpenLog(titleId, null), OpenLogPending);

        MainItems =
        [
            Nav("Home", Icons.Home, null, Pages.Home()),
            Nav("Diary", Icons.Diary, db => db.Watches.Count(), new DiaryPageViewModel(services, watchId => OpenLog(null, watchId))),
            Nav("Watchlist", Icons.Watchlist, db => db.Watchlist.Count(), Pages.Soon("Watchlist", "Titles coming soon and titles out now that you have not seen.", "0.5.0")),
            Nav("On Ice", Icons.OnIce, db => db.OnIce.Count(), Pages.Soon("On Ice", "Things you paused or dropped, with where you stopped and why.", "0.5.0")),
            Nav("People", Icons.People, null, Pages.Soon("People", "Actors and directors you have watched, most watched first.", "0.6.0")),
            Nav("Recap", Icons.Recap, null, Pages.Soon("Recap", "Your year and all time, in one view.", "0.7.0")),
        ];
        LibraryItems =
        [
            Nav("Movies", Icons.Movies, db => db.Titles.Count(t => t.Kind == TitleKind.Movie && db.Watches.Any(w => w.TitleId == t.Id)),
                new LibraryPageViewModel(services, TitleKind.Movie, titleId => OpenLog(titleId, null), "Movies", "No movies watched yet. Search at the top and log one.")),
            Nav("TV", Icons.Tv, db => db.Titles.Count(t => t.Kind == TitleKind.Tv && db.Watches.Any(w => w.TitleId == t.Id)),
                new LibraryPageViewModel(services, TitleKind.Tv, titleId => OpenLog(titleId, null), "TV", "No TV watched yet. Search at the top and log an episode.")),
            Nav("Anime", Icons.Anime, db => db.Titles.Count(t => t.Kind == TitleKind.Anime && db.Watches.Any(w => w.TitleId == t.Id)),
                new LibraryPageViewModel(services, TitleKind.Anime, titleId => OpenLog(titleId, null), "Anime", "No anime watched yet. Search at the top and log a series, film, OVA or more.")),
            Nav("Theaters", Icons.Theaters, db => db.Theaters.Count(), new TheatersPageViewModel(services, OpenTheaterForm)),
        ];
        FooterItems =
        [
            Nav("Export JSON", Icons.Export, null, Pages.Soon("Export JSON", "Save your whole diary as one readable Marquee JSON file.", "0.8.0")),
            Nav("Settings", Icons.Settings, null, new SettingsPageViewModel(services)),
        ];

        RefreshCounts();
        Show(MainItems[0].Page, MainItems[0]);
    }

    public IReadOnlyList<NavItemViewModel> MainItems { get; }
    public IReadOnlyList<NavItemViewModel> LibraryItems { get; }
    public IReadOnlyList<NavItemViewModel> FooterItems { get; }

    private IEnumerable<NavItemViewModel> AllItems => MainItems.Concat(LibraryItems).Concat(FooterItems);

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    /// <summary>The open dialog (Log a watch or a theater form), shown over the page. Null when none is open.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Dialog { get; set; }

    [RelayCommand]
    private void LogWatch() => OpenLog(null, null);

    private void OpenLog(string? titleId, string? watchId) =>
        Dialog = new LogWatchViewModel(_services, titleId, watchId, CloseDialog);

    private void OpenLogPending(FetchedTitle pending) =>
        Dialog = new LogWatchViewModel(_services, pending, CloseDialog);

    private void OpenTheaterForm(Theater? theater) =>
        Dialog = new TheaterFormViewModel(_services.Theaters, theater, saved => CloseDialog(saved is not null));

    private void CloseDialog(bool changed)
    {
        Dialog = null;
        if (!changed)
            return;
        RefreshCounts();
        CurrentPage?.OnShown();
    }

    /// <summary>The top bar search box.</summary>
    [ObservableProperty]
    public partial string TopQuery { get; set; } = "";

    [RelayCommand]
    private void OpenSearch()
    {
        var query = TopQuery.Trim();
        TopQuery = "";
        Show(_search, null);
        if (query.Length > 0)
            _search.SearchFor(query);
    }

    /// <summary>Updates the sidebar counts after the library changes.</summary>
    public void RefreshCounts()
    {
        using var db = _services.Database.CreateContext();
        foreach (var nav in AllItems)
            nav.Refresh(db);
    }

    private void Show(PageViewModel page, NavItemViewModel? item)
    {
        foreach (var nav in AllItems)
            nav.IsActive = nav == item;
        page.OnShown();
        CurrentPage = page;
    }

    private NavItemViewModel Nav(string label, Geometry icon, Func<MarqueeDbContext, int>? count, PageViewModel page) =>
        new(label, icon, count, page, item => Show(item.Page, item));
}

public partial class NavItemViewModel(
    string label, Geometry icon, Func<MarqueeDbContext, int>? count, PageViewModel page, Action<NavItemViewModel> select)
    : ViewModelBase
{
    public string Label { get; } = label;
    public Geometry Icon { get; } = icon;
    public PageViewModel Page { get; } = page;

    /// <summary>Empty when zero, so a fresh diary shows a clean sidebar.</summary>
    [ObservableProperty]
    public partial string Count { get; set; } = "";

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public void Refresh(MarqueeDbContext db)
    {
        var n = count?.Invoke(db) ?? 0;
        Count = n > 0 ? n.ToString("N0") : "";
    }

    [RelayCommand]
    private void Open() => select(this);
}
