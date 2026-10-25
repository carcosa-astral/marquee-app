using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data.Tests;

public sealed class TitleEditTests : IDisposable
{
    private readonly MarqueeFolder _folder =
        MarqueeFolder.AtRoot(Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N")));
    private readonly MarqueeDatabase _database;
    private readonly TitleStore _titles;
    private readonly WatchStore _watches;

    public TitleEditTests()
    {
        _database = new MarqueeDatabase(_folder);
        _database.Initialize();
        _titles = new TitleStore(_database);
        _watches = new WatchStore(_database);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder.Root))
            Directory.Delete(_folder.Root, recursive: true);
    }

    private static FetchedTitle Paprika(int tmdbId = 4977, string overview = "Dream detective.", int runtime = 90) => new FetchedTitle
    {
        Kind = TitleKind.Movie, Format = TitleFormat.Movie, TmdbType = TmdbType.Movie, TmdbId = tmdbId,
        Name = "Paprika", Year = 2006, Runtime = runtime, Overview = overview,
        Credits = [new() { Role = CreditRole.Director, Person = new() { Name = "Satoshi Kon", TmdbId = 1 } }],
    }.WithSource(MetadataSource.Tmdb);

    private static FetchedTitle Series(int episodes) => new FetchedTitle
    {
        Kind = TitleKind.Anime, Format = TitleFormat.Series, AniListId = 7, Name = "Show",
        Seasons = [new() { Number = 1, Episodes = Enumerable.Range(1, episodes).Select(n => new FetchedEpisode { Number = n }).ToList() }],
    }.WithSource(MetadataSource.AniList);

    [Fact]
    public void Edits_win_and_reset_brings_back_the_source_value()
    {
        var t = _titles.Save(Paprika(), SavedImages.None);

        _titles.SaveEdits(t.Id, new Dictionary<string, string?> { [TitleFields.Runtime] = "88", [TitleFields.Name] = "Paprika (Kon)" });

        var edited = _titles.GetDetails(t.Id)!;
        Assert.Equal((88, "Paprika (Kon)"), (edited.Title.RuntimeMinutes!.Value, edited.Title.Name));
        Assert.Equal(MetadataSource.User, edited.Sources[TitleFields.Runtime]);
        Assert.Equal("90", edited.Edits[TitleFields.Runtime].OriginalValue);

        _titles.ResetEdit(t.Id, TitleFields.Runtime);

        var reset = _titles.GetDetails(t.Id)!;
        Assert.Equal(90, reset.Title.RuntimeMinutes);
        Assert.Equal(MetadataSource.Tmdb, reset.Sources[TitleFields.Runtime]);
        Assert.False(reset.Edits.ContainsKey(TitleFields.Runtime));
        Assert.True(reset.Edits.ContainsKey(TitleFields.Name));
    }

    [Fact]
    public void Invalid_edits_are_refused()
    {
        var t = _titles.Save(Paprika(), SavedImages.None);

        Assert.Throws<ArgumentException>(() => _titles.SaveEdits(t.Id, new Dictionary<string, string?> { [TitleFields.Year] = "next year" }));
        Assert.Equal(2006, _titles.GetDetails(t.Id)!.Title.Year);
    }

    [Fact]
    public void A_refresh_keeps_edits_but_updates_what_reset_would_restore()
    {
        var t = _titles.Save(Paprika(), SavedImages.None);
        _titles.SaveEdits(t.Id, new Dictionary<string, string?> { [TitleFields.Runtime] = "88" });

        _titles.Save(Paprika(overview: "Updated overview.", runtime: 91), SavedImages.None);

        var d = _titles.GetDetails(t.Id)!;
        Assert.Equal(88, d.Title.RuntimeMinutes);
        Assert.Equal("Updated overview.", d.Title.Overview);
        Assert.Equal("91", d.Edits[TitleFields.Runtime].OriginalValue);
        _titles.ResetEdit(t.Id, TitleFields.Runtime);
        Assert.Equal(91, _titles.GetDetails(t.Id)!.Title.RuntimeMinutes);
    }

    [Fact]
    public void Fix_match_changes_ids_and_details_but_keeps_watches_and_edits()
    {
        var wrong = _titles.Save(new FetchedTitle
        {
            Kind = TitleKind.Movie, Format = TitleFormat.Movie, TmdbType = TmdbType.Movie, TmdbId = 111, ImdbId = "tt-wrong",
            Name = "Paprika", Year = 1991, Overview = "A different film.",
        }.WithSource(MetadataSource.Tmdb), SavedImages.None);
        _watches.Add(new WatchInput { TitleId = wrong.Id, WatchedOn = new DateOnly(2026, 10, 12), Rating = 5, Note = "Big screen" });
        _titles.SaveEdits(wrong.Id, new Dictionary<string, string?> { [TitleFields.Name] = "Paprika!" });

        _titles.Save(Paprika(), SavedImages.None, rematchTitleId: wrong.Id);

        var d = _titles.GetDetails(wrong.Id)!;
        Assert.Equal((TmdbType.Movie, 4977, (string?)null), (d.Title.TmdbType!.Value, d.Title.TmdbId!.Value, d.Title.ImdbId));
        Assert.Equal((2006, "Dream detective.", "Paprika!"), (d.Title.Year!.Value, d.Title.Overview, d.Title.Name));
        var watch = Assert.Single(_watches.ListForTitle(wrong.Id));
        Assert.Equal((5.0, "Big screen"), (watch.Rating!.Value, watch.Note));
        Assert.Single(_titles.ListAll());
    }

    [Fact]
    public void Fix_match_refuses_a_match_used_by_another_title()
    {
        var a = _titles.Save(Paprika(), SavedImages.None);
        var b = _titles.Save(Paprika(tmdbId: 222), SavedImages.None);

        var ex = Assert.Throws<InvalidOperationException>(() => _titles.Save(Paprika(), SavedImages.None, rematchTitleId: b.Id));
        Assert.Contains("already in your library", ex.Message);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Fix_match_drops_episodes_the_new_match_lacks_unless_watched()
    {
        var t = _titles.Save(Series(5), SavedImages.None);
        var ep4 = _titles.FindEpisodeId(t.Id, 1, 4)!;
        _watches.Add(new WatchInput { TitleId = t.Id, EpisodeId = ep4, WatchedOn = new DateOnly(2026, 1, 1) });

        var replacement = Series(2);
        replacement.AniListId = 8;
        _titles.Save(replacement, SavedImages.None, rematchTitleId: t.Id);

        using var db = _database.CreateContext();
        Assert.Equal([1, 2, 4], db.Episodes.Select(e => e.Number).OrderBy(n => n).ToList());
        Assert.Equal(ep4, db.Watches.Single().EpisodeId);
    }

    [Fact]
    public void Marking_episodes_skips_ones_already_watched()
    {
        var t = _titles.Save(Series(4), SavedImages.None);
        var ids = Enumerable.Range(1, 4).Select(n => _titles.FindEpisodeId(t.Id, 1, n)!).ToList();
        _watches.Add(new WatchInput { TitleId = t.Id, EpisodeId = ids[0], WatchedOn = new DateOnly(2026, 1, 1) });

        var added = _watches.MarkEpisodesWatched(t.Id, ids.Take(3), new DateOnly(2026, 10, 24));

        Assert.Equal(2, added);
        Assert.Equal(3, _watches.ListForTitle(t.Id).Count);
        Assert.Equal(3, _watches.WatchedEpisodeIds(t.Id).Count);
    }

    [Fact]
    public void Shows_are_rated_as_a_whole_and_episodes_are_not_rated()
    {
        var t = _titles.Save(Series(3), SavedImages.None);
        var ep1 = _titles.FindEpisodeId(t.Id, 1, 1)!;

        var w = _watches.Add(new WatchInput { TitleId = t.Id, EpisodeId = ep1, WatchedOn = new DateOnly(2026, 1, 1), Rating = 4 });
        _titles.SetUserRating(t.Id, 4.5);

        Assert.Null(_watches.Get(w.Id)!.Rating);
        Assert.Equal(4.5, _titles.GetDetails(t.Id)!.Title.UserRating);
        Assert.Throws<ArgumentException>(() => _titles.SetUserRating(t.Id, 4.2));

        _titles.Save(Series(3), SavedImages.None);
        Assert.Equal(4.5, _titles.GetDetails(t.Id)!.Title.UserRating);
        _titles.SetUserRating(t.Id, null);
        Assert.Null(_titles.GetDetails(t.Id)!.Title.UserRating);
    }

    [Fact]
    public void People_count_titles_you_have_watched()
    {
        var paprika = _titles.Save(Paprika(), SavedImages.None);
        _titles.Save(Paprika(tmdbId: 5000), SavedImages.None);
        _watches.Add(new WatchInput { TitleId = paprika.Id, WatchedOn = new DateOnly(2026, 1, 1) });
        _watches.Add(new WatchInput { TitleId = paprika.Id, WatchedOn = new DateOnly(2026, 2, 1) });

        using var db = _database.CreateContext();
        var kon = db.People.Single();
        Assert.Equal(1, _titles.WatchedTitleCounts([kon.Id])[kon.Id]);
    }
}
