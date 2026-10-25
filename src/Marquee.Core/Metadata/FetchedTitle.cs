using Marquee.Core.Model;

namespace Marquee.Core.Metadata;

/// <summary>Names of the Title fields that come from TMDB or AniList and can be overridden.</summary>
public static class TitleFields
{
    public const string Name = "name";
    public const string OriginalName = "originalName";
    public const string Year = "year";
    public const string Runtime = "runtime";
    public const string Overview = "overview";
    public const string ReleaseDate = "releaseDate";
    public const string Status = "status";
    public const string Poster = "poster";
    public const string Backdrop = "backdrop";

    public static readonly IReadOnlyList<string> All =
        [Name, OriginalName, Year, Runtime, Overview, ReleaseDate, Status, Poster, Backdrop];
}

/// <summary>
/// A title as fetched from TMDB, AniList or both, before it is saved.
/// Every field that has a value also has an entry in <see cref="Sources"/>.
/// </summary>
public sealed class FetchedTitle
{
    public TitleKind Kind { get; set; }
    public TitleFormat Format { get; set; }

    public TmdbType? TmdbType { get; set; }
    public int? TmdbId { get; set; }
    public int? AniListId { get; set; }
    public int? MalId { get; set; }
    public string? ImdbId { get; set; }

    public string? Name { get; set; }
    public string? OriginalName { get; set; }
    public int? Year { get; set; }
    /// <summary>Minutes. For a series this is the length of one episode.</summary>
    public int? Runtime { get; set; }
    public string? Overview { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? Status { get; set; }
    /// <summary>Remote image address. Saved to data/images when the title is added.</summary>
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }

    /// <summary>TMDB's average user score (0 to 10). Only set when people have voted.</summary>
    public double? TmdbScore { get; set; }
    /// <summary>AniList's average score (0 to 100).</summary>
    public int? AniListScore { get; set; }

    public List<string> Genres { get; set; } = [];
    public List<FetchedCredit> Credits { get; set; } = [];
    public List<FetchedSeason> Seasons { get; set; } = [];

    /// <summary>Which source each field's value came from, keyed by <see cref="TitleFields"/>.</summary>
    public Dictionary<string, MetadataSource> Sources { get; set; } = [];

    /// <summary>Records the source for every field that currently has a value.</summary>
    public FetchedTitle WithSource(MetadataSource source)
    {
        foreach (var field in TitleFields.All)
        {
            if (HasValue(field))
                Sources[field] = source;
        }
        return this;
    }

    public bool HasValue(string field) => field switch
    {
        TitleFields.Name => !string.IsNullOrWhiteSpace(Name),
        TitleFields.OriginalName => !string.IsNullOrWhiteSpace(OriginalName),
        TitleFields.Year => Year is not null,
        TitleFields.Runtime => Runtime is > 0,
        TitleFields.Overview => !string.IsNullOrWhiteSpace(Overview),
        TitleFields.ReleaseDate => ReleaseDate is not null,
        TitleFields.Status => !string.IsNullOrWhiteSpace(Status),
        TitleFields.Poster => !string.IsNullOrWhiteSpace(PosterUrl),
        TitleFields.Backdrop => !string.IsNullOrWhiteSpace(BackdropUrl),
        _ => false,
    };
}

public sealed class FetchedPerson
{
    public string Name { get; set; } = "";
    public int? TmdbId { get; set; }
    public int? AniListId { get; set; }
    public string? PhotoUrl { get; set; }
    public DateOnly? BirthDate { get; set; }
}

public sealed class FetchedCredit
{
    public FetchedPerson Person { get; set; } = new();
    public CreditRole Role { get; set; }
    public string? Character { get; set; }
    public int Order { get; set; }
}

public sealed class FetchedSeason
{
    public int Number { get; set; }
    public string? Name { get; set; }
    public List<FetchedEpisode> Episodes { get; set; } = [];
}

public sealed class FetchedEpisode
{
    public int Number { get; set; }
    public string? Name { get; set; }
    public DateOnly? AirDate { get; set; }
    public int? Runtime { get; set; }
}
