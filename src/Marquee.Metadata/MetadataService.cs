using Marquee.Core.Metadata;

namespace Marquee.Metadata;

/// <summary>What a search found, plus any source that could not be searched and why.</summary>
public sealed record SearchOutcome(List<SearchResult> Results, string? TmdbProblem, string? AniListProblem);

/// <summary>Searches both sources, merges the results, and fetches full details for one result.</summary>
public sealed class MetadataService(TmdbClient tmdb, AniListClient aniList)
{
    public bool HasTmdbKey => tmdb.HasKey;

    /// <summary>Searches TMDB and AniList at the same time. One failing source never hides the other.</summary>
    public async Task<SearchOutcome> SearchAsync(string query, CancellationToken ct = default)
    {
        var tmdbTask = tmdb.HasKey
            ? Try(() => tmdb.SearchAsync(query, ct))
            : Task.FromResult<(List<SearchHit>, string?)>(([], "Add your TMDB key in Settings to search movies and TV."));
        var aniListTask = Try(() => aniList.SearchAsync(query, ct));
        await Task.WhenAll(tmdbTask, aniListTask);

        var (tmdbHits, tmdbProblem) = tmdbTask.Result;
        var (aniListHits, aniListProblem) = aniListTask.Result;
        return new SearchOutcome(SearchMerger.Merge(tmdbHits, aniListHits), tmdbProblem, aniListProblem);
    }

    /// <summary>
    /// Fetches full details. For anime, AniList leads and TMDB fills gaps; when the search
    /// did not already pair the anime with a TMDB entry, a TMDB match is looked for here.
    /// Filling from TMDB is best effort and never stops a title from being added.
    /// </summary>
    public async Task<FetchedTitle> FetchAsync(SearchResult result, CancellationToken ct = default)
    {
        if (result.AniList is null)
        {
            var hit = result.Tmdb!;
            return await tmdb.GetAsync(hit.TmdbType!.Value, hit.Id, ct);
        }

        var aniListTask = aniList.GetAsync(result.AniList.Id, ct);
        var tmdbTask = tmdb.HasKey ? FindTmdbFillAsync(result, ct) : Task.FromResult<FetchedTitle?>(null);
        var fill = await tmdbTask;
        return MetadataMerge.Combine(fill, await aniListTask);
    }

    private async Task<FetchedTitle?> FindTmdbFillAsync(SearchResult result, CancellationToken ct)
    {
        try
        {
            var hit = result.Tmdb;
            if (hit is null)
            {
                var al = result.AniList!;
                foreach (var name in al.Names.Take(2))
                {
                    hit = (await tmdb.SearchAsync(name, ct)).FirstOrDefault(t => TitleMatcher.IsSameTitle(al, t));
                    if (hit is not null)
                        break;
                }
            }
            return hit is null ? null : await tmdb.GetAsync(hit.TmdbType!.Value, hit.Id, ct);
        }
        catch (MetadataException)
        {
            return null;
        }
    }

    private static async Task<(List<SearchHit> Hits, string? Problem)> Try(Func<Task<List<SearchHit>>> search)
    {
        try
        {
            return (await search(), null);
        }
        catch (MetadataException ex)
        {
            return ([], ex.Message);
        }
    }
}
