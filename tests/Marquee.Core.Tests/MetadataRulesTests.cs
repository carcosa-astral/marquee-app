using Marquee.Core.Metadata;
using Marquee.Core.Model;

namespace Marquee.Core.Tests;

public class MetadataRulesTests
{
    private static SearchHit AniList(string name, int year, TitleFormat format, int rank = 0, int? mal = null, params string[] names) => new()
    {
        Source = MetadataSource.AniList, Id = 100 + rank, Kind = TitleKind.Anime, Format = format,
        Name = name, Names = [name, .. names], Year = year, Rank = rank, MalId = mal,
    };

    private static SearchHit Tmdb(string name, int year, TitleFormat format, TitleKind kind, int rank = 0, params string[] names) => new()
    {
        Source = MetadataSource.Tmdb, Id = 200 + rank, TmdbType = format == TitleFormat.Series ? TmdbType.Tv : TmdbType.Movie,
        Kind = kind, Format = format, Name = name, Names = [name, .. names], Year = year, Rank = rank,
    };

    [Fact]
    public void Same_name_year_and_format_merge_into_one_result()
    {
        var results = SearchMerger.Merge(
            [Tmdb("Paprika", 1991, TitleFormat.Movie, TitleKind.Movie, 0), Tmdb("Paprika", 2006, TitleFormat.Film, TitleKind.Anime, 1)],
            [AniList("Paprika", 2006, TitleFormat.Film, 0)]);

        Assert.Equal(2, results.Count);
        var merged = Assert.Single(results, r => r.IsMerged);
        Assert.Equal(2006, merged.Tmdb!.Year);
        Assert.Equal(TitleKind.Anime, merged.Kind);
        Assert.Equal(1991, Assert.Single(results, r => !r.IsMerged).Tmdb!.Year);
    }

    [Fact]
    public void Names_match_ignoring_case_accents_and_punctuation()
    {
        var results = SearchMerger.Merge(
            [Tmdb("Frieren: Beyond Journey's End", 2023, TitleFormat.Series, TitleKind.Anime, 0, "葬送のフリーレン")],
            [AniList("Frieren: Beyond Journey’s End", 2023, TitleFormat.Series, 0, null, "Sousou no Frieren", "葬送のフリーレン")]);

        Assert.True(Assert.Single(results).IsMerged);
    }

    [Fact]
    public void Different_year_or_format_do_not_merge()
    {
        Assert.Equal(2, SearchMerger.Merge(
            [Tmdb("Akira", 1988, TitleFormat.Movie, TitleKind.Anime)],
            [AniList("Akira", 1989, TitleFormat.Film)]).Count);

        Assert.Equal(2, SearchMerger.Merge(
            [Tmdb("Hellsing", 2006, TitleFormat.Series, TitleKind.Anime)],
            [AniList("Hellsing", 2006, TitleFormat.Film)]).Count);
    }

    [Fact]
    public void An_ova_can_match_a_tmdb_series()
    {
        Assert.True(TitleMatcher.FormatsCompatible(TitleFormat.Ova, TitleFormat.Series));
        Assert.False(TitleMatcher.FormatsCompatible(TitleFormat.Film, TitleFormat.Series));
    }

    [Fact]
    public void Results_are_ordered_by_the_best_rank_from_either_source()
    {
        var results = SearchMerger.Merge(
            [Tmdb("Heat", 1995, TitleFormat.Movie, TitleKind.Movie, 0)],
            [AniList("Heat Guy J", 2002, TitleFormat.Series, 0), AniList("Other", 2001, TitleFormat.Series, 1)]);

        Assert.Equal(["Heat Guy J", "Heat", "Other"], results.Select(r => r.Primary.Name));
    }

    private static FetchedTitle Fetched(MetadataSource source, Action<FetchedTitle> set)
    {
        var t = new FetchedTitle { Kind = TitleKind.Anime, Format = TitleFormat.Film };
        set(t);
        return t.WithSource(source);
    }

    [Fact]
    public void Anime_uses_AniList_first_and_TMDB_fills_gaps()
    {
        var aniList = Fetched(MetadataSource.AniList, t =>
        {
            t.AniListId = 1943; t.MalId = 1943;
            t.Name = "Paprika"; t.Overview = "AniList overview"; t.PosterUrl = "https://al/poster.jpg"; t.Runtime = 90;
            t.Genres = ["Sci-Fi"];
        });
        var tmdb = Fetched(MetadataSource.Tmdb, t =>
        {
            t.TmdbId = 4977; t.TmdbType = TmdbType.Movie; t.ImdbId = "tt0851578";
            t.Name = "Paprika"; t.Overview = "TMDB overview"; t.BackdropUrl = "https://tmdb/backdrop.jpg"; t.Status = "Released";
            t.Genres = ["Animation"];
        });

        var merged = MetadataMerge.Combine(tmdb, aniList);

        Assert.Equal("AniList overview", merged.Overview);
        Assert.Equal(MetadataSource.AniList, merged.Sources[TitleFields.Overview]);
        Assert.Equal("https://tmdb/backdrop.jpg", merged.BackdropUrl);
        Assert.Equal(MetadataSource.Tmdb, merged.Sources[TitleFields.Backdrop]);
        Assert.Equal(MetadataSource.Tmdb, merged.Sources[TitleFields.Status]);
        Assert.Equal((4977, 1943, "tt0851578"), (merged.TmdbId!.Value, merged.AniListId!.Value, merged.ImdbId));
        Assert.Equal(["Sci-Fi"], merged.Genres);
        Assert.Equal(TitleKind.Anime, merged.Kind);
    }

    [Fact]
    public void Movies_and_TV_use_TMDB_only()
    {
        var tmdb = Fetched(MetadataSource.Tmdb, t => t.Name = "Heat");

        Assert.Same(tmdb, MetadataMerge.Combine(tmdb, null));
    }

    [Fact]
    public void Fill_adds_missing_photos_and_directors_from_the_fallback()
    {
        var aniList = Fetched(MetadataSource.AniList, t => t.Credits =
        [
            new() { Role = CreditRole.Voice, Person = new() { Name = "Megumi Hayashibara", AniListId = 1 } },
        ]);
        var tmdb = Fetched(MetadataSource.Tmdb, t => t.Credits =
        [
            new() { Role = CreditRole.Director, Person = new() { Name = "Satoshi Kon", TmdbId = 9 } },
            new() { Role = CreditRole.Voice, Person = new() { Name = "Megumi Hayashibara", TmdbId = 2, PhotoUrl = "https://tmdb/m.jpg" } },
        ]);

        var merged = MetadataMerge.Fill(aniList, tmdb);

        var voice = Assert.Single(merged.Credits, c => c.Role == CreditRole.Voice);
        Assert.Equal("https://tmdb/m.jpg", voice.Person.PhotoUrl);
        Assert.Equal(2, voice.Person.TmdbId);
        Assert.Equal("Satoshi Kon", Assert.Single(merged.Credits, c => c.Role == CreditRole.Director).Person.Name);
    }

    [Theory]
    [InlineData("Neil McCauley", true)]
    [InlineData(null, true)]
    [InlineData("Self", false)]
    [InlineData("Himself", false)]
    [InlineData("Self - Host", false)]
    [InlineData("Lt. Vincent Hanna (archive footage)", false)]
    [InlineData("Selfridge", true)]
    public void Self_and_archive_footage_do_not_count(string? character, bool counted) =>
        Assert.Equal(counted, CreditRules.IsCountedCastRole(character));
}
