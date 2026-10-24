using Marquee.Core.Model;

namespace Marquee.Core.Metadata;

/// <summary>
/// Metadata precedence from the build plan:
/// movies and TV use TMDB first; anime uses AniList first and TMDB fills what is missing,
/// except the poster, where TMDB's art is preferred for anime too.
/// User overrides beat both, and are applied when saving.
/// </summary>
public static class MetadataMerge
{
    /// <summary>Combines what each source returned for the same title.</summary>
    public static FetchedTitle Combine(FetchedTitle? tmdb, FetchedTitle? aniList)
    {
        if (aniList is not null)
            return tmdb is null ? aniList : PreferTmdbPoster(Fill(aniList, tmdb), tmdb);
        return tmdb ?? throw new ArgumentException("At least one source is needed.");
    }

    /// <summary>The owner prefers TMDB's poster art for anime, so it replaces AniList's cover when TMDB has one.</summary>
    private static FetchedTitle PreferTmdbPoster(FetchedTitle merged, FetchedTitle tmdb)
    {
        if (tmdb.HasValue(TitleFields.Poster))
        {
            merged.PosterUrl = tmdb.PosterUrl;
            merged.Sources[TitleFields.Poster] = MetadataSource.Tmdb;
        }
        return merged;
    }

    /// <summary>
    /// Keeps everything the primary source has and fills empty fields from the fallback,
    /// remembering where each value came from. Seasons are never filled, because TMDB and
    /// AniList split anime seasons differently.
    /// </summary>
    public static FetchedTitle Fill(FetchedTitle primary, FetchedTitle fallback)
    {
        var merged = new FetchedTitle
        {
            Kind = primary.Kind,
            Format = primary.Format,
            TmdbType = primary.TmdbType ?? fallback.TmdbType,
            TmdbId = primary.TmdbId ?? fallback.TmdbId,
            AniListId = primary.AniListId ?? fallback.AniListId,
            MalId = primary.MalId ?? fallback.MalId,
            ImdbId = primary.ImdbId ?? fallback.ImdbId,
            Genres = primary.Genres.Count > 0 ? [.. primary.Genres] : [.. fallback.Genres],
            Seasons = primary.Seasons,
            Sources = new(primary.Sources),
        };

        foreach (var field in TitleFields.All)
        {
            var from = primary.HasValue(field) ? primary : fallback.HasValue(field) ? fallback : null;
            if (from is null)
                continue;
            Copy(field, from, merged);
            if (from.Sources.TryGetValue(field, out var source))
                merged.Sources[field] = source;
        }

        merged.Credits = MergeCredits(primary.Credits, fallback.Credits);
        return merged;
    }

    private static List<FetchedCredit> MergeCredits(List<FetchedCredit> primary, List<FetchedCredit> fallback)
    {
        var result = primary.ToList();

        // Fill missing photos (and TMDB ids) from the fallback when the same person appears in both.
        var byName = fallback
            .GroupBy(c => TitleMatcher.Normalize(c.Person.Name))
            .ToDictionary(g => g.Key, g => g.First().Person);
        foreach (var credit in result)
        {
            if (!byName.TryGetValue(TitleMatcher.Normalize(credit.Person.Name), out var other))
                continue;
            credit.Person.PhotoUrl ??= other.PhotoUrl;
            credit.Person.TmdbId ??= other.TmdbId;
            credit.Person.AniListId ??= other.AniListId;
            credit.Person.BirthDate ??= other.BirthDate;
        }

        // If the primary source lists no director, use the fallback's.
        if (!result.Any(c => c.Role == CreditRole.Director))
            result.AddRange(fallback.Where(c => c.Role == CreditRole.Director));

        return result;
    }

    private static void Copy(string field, FetchedTitle from, FetchedTitle to)
    {
        switch (field)
        {
            case TitleFields.Name: to.Name = from.Name; break;
            case TitleFields.OriginalName: to.OriginalName = from.OriginalName; break;
            case TitleFields.Year: to.Year = from.Year; break;
            case TitleFields.Runtime: to.Runtime = from.Runtime; break;
            case TitleFields.Overview: to.Overview = from.Overview; break;
            case TitleFields.ReleaseDate: to.ReleaseDate = from.ReleaseDate; break;
            case TitleFields.Status: to.Status = from.Status; break;
            case TitleFields.Poster: to.PosterUrl = from.PosterUrl; break;
            case TitleFields.Backdrop: to.BackdropUrl = from.BackdropUrl; break;
        }
    }
}
