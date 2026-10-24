using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Data;
using Marquee.Metadata;

namespace Marquee.App.ViewModels;

public enum SearchFilter { All, Movies, Tv, Anime }

/// <summary>"Add a title": searches TMDB and AniList together and adds results to the library.</summary>
public sealed partial class SearchPageViewModel(AppServices services, Action onLibraryChanged) : PageViewModel
{
    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(450);

    private CancellationTokenSource? _search;
    private List<SearchResultViewModel> _all = [];
    private bool _searched;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAll), nameof(IsMovies), nameof(IsTv), nameof(IsAnime))]
    public partial SearchFilter Filter { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial string? CountText { get; set; }

    [ObservableProperty]
    public partial string? Problem { get; set; }

    [ObservableProperty]
    public partial string SourcesText { get; set; } = "";

    /// <summary>A short line shown when there are no results to list.</summary>
    [ObservableProperty]
    public partial string? Hint { get; set; } = TypeHint;

    private const string TypeHint = "Type a title to search movies, TV and anime.";

    public ObservableCollection<SearchResultViewModel> Results { get; } = [];

    public bool IsAll => Filter == SearchFilter.All;
    public bool IsMovies => Filter == SearchFilter.Movies;
    public bool IsTv => Filter == SearchFilter.Tv;
    public bool IsAnime => Filter == SearchFilter.Anime;

    public override void OnShown() => SourcesText = services.Metadata.HasTmdbKey
        ? "Searching TMDB and AniList"
        : "Searching AniList only. Add a TMDB key in Settings for movies and TV.";

    /// <summary>Opens the page with a query typed elsewhere, such as the top bar.</summary>
    public void SearchFor(string query)
    {
        Query = query;
        _ = RunAsync(query, delay: false);
    }

    partial void OnQueryChanged(string value) => _ = RunAsync(value, delay: true);

    partial void OnFilterChanged(SearchFilter value) => ApplyFilter();

    [RelayCommand]
    private Task SearchNow() => RunAsync(Query, delay: false);

    [RelayCommand]
    private void SetFilter(SearchFilter filter) => Filter = filter;

    private async Task RunAsync(string query, bool delay)
    {
        _search?.Cancel();
        var cts = _search = new CancellationTokenSource();
        query = query.Trim();
        if (query.Length < 2)
        {
            _searched = false;
            _all = [];
            ApplyFilter();
            CountText = null;
            Problem = null;
            IsSearching = false;
            return;
        }

        try
        {
            if (delay)
                await Task.Delay(TypingDelay, cts.Token);
            IsSearching = true;
            var outcome = await services.Metadata.SearchAsync(query, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            _searched = true;
            var library = services.Titles.GetLibraryIds();
            _all = outcome.Results
                .Select(r => new SearchResultViewModel(r, library.Contains(r), services, onLibraryChanged))
                .ToList();
            // A missing TMDB key is already explained next to the search box.
            var problems = new[] { outcome.AniListProblem, services.Metadata.HasTmdbKey ? outcome.TmdbProblem : null }.OfType<string>().ToList();
            Problem = problems.Count == 0 ? null : string.Join(" ", problems);
            ApplyFilter();
            OnShown();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_search == cts)
                IsSearching = false;
        }
    }

    private void ApplyFilter()
    {
        Results.Clear();
        foreach (var r in _all.Where(r => Matches(r.Kind)))
        {
            Results.Add(r);
            r.LoadPoster();
        }
        CountText = _all.Count == 0 ? null : Results.Count == 1 ? "1 RESULT" : $"{Results.Count} RESULTS";
        Hint = !_searched ? TypeHint
            : _all.Count == 0 ? "Nothing found. Try another spelling or the original title."
            : Results.Count == 0 ? "No results of this kind. Try All."
            : null;
    }

    private bool Matches(TitleKind kind) => Filter switch
    {
        SearchFilter.Movies => kind == TitleKind.Movie,
        SearchFilter.Tv => kind == TitleKind.Tv,
        SearchFilter.Anime => kind == TitleKind.Anime,
        _ => true,
    };
}

public sealed partial class SearchResultViewModel : ViewModelBase
{
    private readonly SearchResult _result;
    private readonly AppServices _services;
    private readonly Action _onLibraryChanged;
    private bool _posterRequested;

    public SearchResultViewModel(SearchResult result, bool inLibrary, AppServices services, Action onLibraryChanged)
    {
        _result = result;
        _services = services;
        _onLibraryChanged = onLibraryChanged;
        IsInLibrary = inLibrary;

        var hit = result.Primary;
        Name = hit.Name;
        YearText = hit.Year?.ToString() ?? "";
        Kind = result.Kind;
        KindLabel = Label(result.Kind, hit.Format);
        Meta = string.Join(" · ", MetaParts(result));
        Overview = (result.Tmdb?.Overview ?? hit.Overview)?.ReplaceLineEndings(" ");
    }

    public string Name { get; }
    public string YearText { get; }
    public TitleKind Kind { get; }
    public string KindLabel { get; }
    public string Meta { get; }
    public string? Overview { get; }

    public bool IsMovie => Kind == TitleKind.Movie;
    public bool IsTv => Kind == TitleKind.Tv;
    public bool IsAnime => Kind == TitleKind.Anime;
    public bool FromTmdb => _result.Tmdb is not null;
    public bool FromAniList => _result.AniList is not null;
    public bool IsMerged => _result.IsMerged;

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddLabel))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial bool IsInLibrary { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddLabel))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial bool IsAdding { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    public string AddLabel => IsAdding ? "Adding..." : IsInLibrary ? "In your library" : "Add to library";

    public async void LoadPoster()
    {
        if (_posterRequested)
            return;
        _posterRequested = true;
        Poster = await _services.Images.LoadRemoteAsync(_result.Primary.PosterUrl ?? _result.Tmdb?.PosterUrl, 144);
    }

    private bool CanAdd() => !IsInLibrary && !IsAdding;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task Add()
    {
        IsAdding = true;
        Error = null;
        try
        {
            var fetched = await _services.Metadata.FetchAsync(_result);
            var images = await _services.Downloader.SaveAllAsync(fetched);
            await Task.Run(() => _services.Titles.Save(fetched, images));
            IsInLibrary = true;
            _onLibraryChanged();
        }
        catch (MetadataException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Error = "Could not add this title: " + ex.Message;
        }
        finally
        {
            IsAdding = false;
        }
    }

    private static IEnumerable<string> MetaParts(SearchResult r)
    {
        var hit = r.Primary;
        if (hit.Episodes is > 1)
            yield return $"{hit.Episodes} episodes";
        if (hit.Runtime is > 0)
            yield return $"{hit.Runtime} min";
        var genres = hit.Genres.Count > 0 ? hit.Genres : r.Tmdb?.Genres ?? [];
        if (genres.Count > 0)
            yield return string.Join(", ", genres.Take(3));
    }

    private static string Label(TitleKind kind, TitleFormat format) => kind switch
    {
        TitleKind.Movie => "Movie",
        TitleKind.Tv => "TV",
        _ => format switch
        {
            TitleFormat.Film or TitleFormat.Movie => "Anime film",
            TitleFormat.Ova => "Anime OVA",
            TitleFormat.Ona => "Anime ONA",
            TitleFormat.Special => "Anime special",
            _ => "Anime series",
        },
    };
}
