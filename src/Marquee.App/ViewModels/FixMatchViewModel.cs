using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Metadata;

namespace Marquee.App.ViewModels;

/// <summary>
/// Point a title at a different TMDB or AniList entry. Diary entries, ratings and edits stay.
/// Matches the "Fix the match" panel in docs/design/mocks/EditMeta.dc.html.
/// </summary>
public sealed partial class FixMatchViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly string _titleId;
    private readonly Action<bool> _close;
    private readonly Title _title;
    private CancellationTokenSource? _search;

    public FixMatchViewModel(AppServices services, string titleId, Action<bool> close)
    {
        _services = services;
        _titleId = titleId;
        _close = close;
        _title = services.Titles.GetDetails(titleId)!.Title;
        Query = _title.Name;
        _ = SearchAsync();
    }

    public ObservableCollection<MatchCandidate> Candidates { get; } = [];

    [ObservableProperty] public partial string Query { get; set; }
    [ObservableProperty] public partial bool IsSearching { get; set; }
    [ObservableProperty] public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseCommand))]
    public partial MatchCandidate? Selected { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseCommand))]
    [NotifyPropertyChangedFor(nameof(UseLabel))]
    public partial bool IsSaving { get; set; }

    public string UseLabel => IsSaving ? "Updating..." : "Use this match";

    [RelayCommand]
    private Task Search() => SearchAsync();

    private async Task SearchAsync()
    {
        _search?.Cancel();
        var cts = _search = new CancellationTokenSource();
        var q = Query.Trim();
        if (q.Length < 2)
            return;
        IsSearching = true;
        Error = null;
        try
        {
            var outcome = await _services.Metadata.SearchAsync(q, cts.Token);
            if (cts.IsCancellationRequested)
                return;
            Candidates.Clear();
            foreach (var r in outcome.Results.Take(8))
            {
                var c = new MatchCandidate(r, IsCurrent(r), Pick);
                Candidates.Add(c);
                c.LoadPoster(_services.Images);
            }
            Pick(Candidates.FirstOrDefault(c => c.IsCurrent) ?? Candidates.FirstOrDefault());
            if (Candidates.Count == 0)
                Error = outcome.AniListProblem ?? outcome.TmdbProblem ?? "Nothing found. Try another spelling or the original title.";
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

    private bool IsCurrent(SearchResult r) =>
        (r.AniList is { } al && al.Id == _title.AniListId)
        || (r.Tmdb is { } t && t.Id == _title.TmdbId && t.TmdbType == _title.TmdbType);

    private void Pick(MatchCandidate? candidate)
    {
        foreach (var c in Candidates)
            c.IsSelected = c == candidate;
        Selected = candidate;
    }

    private bool CanUse() => Selected is not null && !Selected.IsCurrent && !IsSaving;

    [RelayCommand(CanExecute = nameof(CanUse))]
    private async Task Use()
    {
        IsSaving = true;
        Error = null;
        try
        {
            var fetched = await _services.Metadata.FetchAsync(Selected!.Result);
            var images = await _services.Downloader.SaveAllAsync(fetched);
            await Task.Run(() => _services.Titles.Save(fetched, images, rematchTitleId: _titleId));
            await _services.ApplyImdbRatingsAsync(_titleId);
            _close(true);
        }
        catch (Exception ex) when (ex is MetadataException or InvalidOperationException)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Error = "Could not change the match: " + ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _close(false);
}

public sealed partial class MatchCandidate(SearchResult result, bool isCurrent, Action<MatchCandidate> pick) : ViewModelBase
{
    public SearchResult Result { get; } = result;
    public bool IsCurrent { get; } = isCurrent;
    public string Name { get; } = result.Primary.Name;
    public string Year { get; } = result.Primary.Year?.ToString() ?? "";

    public string Meta { get; } = string.Join(" · ",
        SearchResultViewModel.Label(result.Kind, result.Primary.Format),
        result.IsMerged ? "AniList and TMDB" : result.AniList is not null ? "AniList only" : "TMDB only");

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    public async void LoadPoster(ImageLoader images) =>
        Poster = await images.LoadRemoteAsync(Result.Tmdb?.PosterUrl ?? Result.Primary.PosterUrl, 68);

    [RelayCommand]
    private void Pick() => pick(this);
}
