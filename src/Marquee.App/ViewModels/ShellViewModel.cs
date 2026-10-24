using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Model;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>The main window: sidebar, top bar and the current page.</summary>
public partial class ShellViewModel : ViewModelBase
{
    public ShellViewModel(MarqueeDatabase database, SettingsStore settings)
    {
        using var db = database.CreateContext();
        int Titles(TitleKind kind) => db.Titles.Count(t => t.Kind == kind);

        MainItems =
        [
            Nav("Home", Icons.Home, 0, Pages.Home()),
            Nav("Diary", Icons.Diary, db.Watches.Count(), Pages.Soon("Diary", "Every watch, grouped by month, with filters.", "0.3.0")),
            Nav("Watchlist", Icons.Watchlist, db.Watchlist.Count(), Pages.Soon("Watchlist", "Titles coming soon and titles out now that you have not seen.", "0.5.0")),
            Nav("On Ice", Icons.OnIce, db.OnIce.Count(), Pages.Soon("On Ice", "Things you paused or dropped, with where you stopped and why.", "0.5.0")),
            Nav("People", Icons.People, 0, Pages.Soon("People", "Actors and directors you have watched, most watched first.", "0.6.0")),
            Nav("Recap", Icons.Recap, 0, Pages.Soon("Recap", "Your year and all time, in one view.", "0.7.0")),
        ];
        LibraryItems =
        [
            Nav("Movies", Icons.Movies, Titles(TitleKind.Movie), Pages.Soon("Movies", "Every movie in your library.", "0.4.0")),
            Nav("TV", Icons.Tv, Titles(TitleKind.Tv), Pages.Soon("TV", "Every TV show in your library.", "0.4.0")),
            Nav("Anime", Icons.Anime, Titles(TitleKind.Anime), Pages.Soon("Anime", "Series, films, OVAs, ONAs and specials.", "0.4.0")),
            Nav("Theaters", Icons.Theaters, db.Theaters.Count(), Pages.Soon("Theaters", "The theaters you add, with visits, formats and ratings.", "0.3.0")),
        ];
        FooterItems =
        [
            Nav("Export JSON", Icons.Export, 0, Pages.Soon("Export JSON", "Save your whole diary as one readable Marquee JSON file.", "0.8.0")),
            Nav("Settings", Icons.Settings, 0, Pages.Settings(database.Folder, settings)),
        ];

        Select(MainItems[0]);
    }

    public IReadOnlyList<NavItemViewModel> MainItems { get; }
    public IReadOnlyList<NavItemViewModel> LibraryItems { get; }
    public IReadOnlyList<NavItemViewModel> FooterItems { get; }

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    private void Select(NavItemViewModel item)
    {
        foreach (var nav in MainItems.Concat(LibraryItems).Concat(FooterItems))
            nav.IsActive = nav == item;
        CurrentPage = item.Page;
    }

    private NavItemViewModel Nav(string label, Geometry icon, int count, PageViewModel page) =>
        new(label, icon, count, page, Select);
}

public partial class NavItemViewModel(string label, Geometry icon, int count, PageViewModel page, Action<NavItemViewModel> select)
    : ViewModelBase
{
    public string Label { get; } = label;
    public Geometry Icon { get; } = icon;

    /// <summary>Empty when zero, so a fresh diary shows a clean sidebar.</summary>
    public string Count { get; } = count > 0 ? count.ToString("N0") : "";

    public PageViewModel Page { get; } = page;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [RelayCommand]
    private void Open() => select(this);
}
