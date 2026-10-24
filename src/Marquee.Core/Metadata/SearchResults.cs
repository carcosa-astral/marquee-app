using Marquee.Core.Model;

namespace Marquee.Core.Metadata;

/// <summary>One search result from one source.</summary>
public sealed class SearchHit
{
    public MetadataSource Source { get; init; }
    public int Id { get; init; }
    /// <summary>Only for TMDB hits.</summary>
    public TmdbType? TmdbType { get; init; }
    public int? MalId { get; init; }

    public TitleKind Kind { get; init; }
    public TitleFormat Format { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Every name the source knows, used for matching across sources.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
    public int? Year { get; init; }
    public string? Overview { get; init; }
    public string? PosterUrl { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public int? Episodes { get; init; }
    public int? Runtime { get; init; }
    /// <summary>Position in the source's own result list, best first.</summary>
    public int Rank { get; init; }
}

/// <summary>A search result shown to the user, merged across sources when they match.</summary>
public sealed class SearchResult
{
    public SearchHit? Tmdb { get; init; }
    public SearchHit? AniList { get; init; }

    /// <summary>The hit whose details lead: AniList for anime, otherwise TMDB.</summary>
    public SearchHit Primary => AniList ?? Tmdb!;
    public bool IsMerged => Tmdb is not null && AniList is not null;
    public TitleKind Kind => AniList is not null ? TitleKind.Anime : Tmdb!.Kind;
}

public static class SearchMerger
{
    /// <summary>
    /// Pairs TMDB and AniList hits that are the same title, then orders everything by
    /// the best rank either source gave it.
    /// </summary>
    public static List<SearchResult> Merge(IEnumerable<SearchHit> tmdb, IEnumerable<SearchHit> aniList)
    {
        var tmdbLeft = tmdb.ToList();
        var results = new List<(SearchResult Result, int Rank)>();

        foreach (var al in aniList)
        {
            var match = tmdbLeft.FirstOrDefault(t => TitleMatcher.IsSameTitle(al, t));
            if (match is not null)
                tmdbLeft.Remove(match);
            var rank = Math.Min(al.Rank, match?.Rank ?? int.MaxValue);
            results.Add((new SearchResult { AniList = al, Tmdb = match }, rank));
        }
        results.AddRange(tmdbLeft.Select(t => (new SearchResult { Tmdb = t }, t.Rank)));

        return results
            .OrderBy(r => r.Rank)
            .ThenBy(r => r.Result.AniList is null ? 1 : 0)
            .Select(r => r.Result)
            .ToList();
    }
}
