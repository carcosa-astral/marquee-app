using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data.Tests;

public sealed class TitleStoreTests : IDisposable
{
    private readonly MarqueeFolder _folder =
        MarqueeFolder.AtRoot(Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N")));
    private readonly MarqueeDatabase _database;
    private readonly TitleStore _store;

    public TitleStoreTests()
    {
        _database = new MarqueeDatabase(_folder);
        _database.Initialize();
        _store = new TitleStore(_database);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder.Root))
            Directory.Delete(_folder.Root, recursive: true);
    }

    private static FetchedTitle Frieren(string overview = "Elf mage.") => new FetchedTitle
    {
        Kind = TitleKind.Anime,
        Format = TitleFormat.Series,
        AniListId = 154587,
        MalId = 52991,
        Name = "Frieren",
        Overview = overview,
        Year = 2023,
        Runtime = 24,
        PosterUrl = "https://al/xl.jpg",
        Genres = ["Adventure", "Fantasy"],
        Credits =
        [
            new() { Role = CreditRole.Director, Person = new() { Name = "Keiichirou Saitou", AniListId = 1 } },
            new() { Role = CreditRole.Voice, Character = "Frieren", Person = new() { Name = "Atsumi Tanezaki", AniListId = 2, PhotoUrl = "https://al/a.jpg" } },
        ],
        Seasons = [new() { Number = 1, Episodes = [new() { Number = 1 }, new() { Number = 2 }] }],
    }.WithSource(MetadataSource.AniList);

    private static readonly SavedImages Images = new()
    {
        PosterPath = "images/titles/anilist-154587-poster.jpg",
        PersonPhotos = { ["https://al/a.jpg"] = "images/people/anilist-2.jpg" },
    };

    [Fact]
    public void Saving_stores_the_title_with_its_sources_people_and_episodes()
    {
        var saved = _store.Save(Frieren(), Images);

        using var db = _database.CreateContext();
        var title = db.Titles.Single();
        Assert.Equal(saved.Id, title.Id);
        Assert.Equal(("Frieren", 2023, "images/titles/anilist-154587-poster.jpg"), (title.Name, title.Year!.Value, title.PosterPath));
        Assert.Equal(MetadataSource.AniList, db.FieldSources.Single(s => s.FieldName == TitleFields.Overview).Source);
        Assert.DoesNotContain(db.FieldSources, s => s.FieldName == TitleFields.Backdrop);
        Assert.Equal(["Adventure", "Fantasy"], db.Genres.Select(g => g.Name).OrderBy(n => n));
        Assert.Equal(2, db.Credits.Count());
        Assert.Equal("images/people/anilist-2.jpg", db.People.Single(p => p.AniListId == 2).PhotoPath);
        Assert.Equal(2, db.Episodes.Count());
        Assert.Contains((154587), _store.GetLibraryIds().AniList);
    }

    [Fact]
    public void Refreshing_keeps_overrides_watches_and_episode_links()
    {
        var title = _store.Save(Frieren(), Images);
        using (var db = _database.CreateContext())
        {
            var episode = db.Episodes.Single(e => e.Number == 1);
            db.FieldOverrides.Add(new FieldOverride { TitleId = title.Id, FieldName = TitleFields.Name, Value = "Sousou no Frieren" });
            db.Titles.Single().Name = "Sousou no Frieren";
            db.Watches.Add(new Watch { TitleId = title.Id, EpisodeId = episode.Id, WatchedOn = new DateOnly(2026, 1, 2), Rating = 5, Note = "Lovely" });
            db.SaveChanges();
        }

        var refreshed = Frieren("New overview from AniList.");
        refreshed.Seasons[0].Episodes.Add(new FetchedEpisode { Number = 3 });
        _store.Save(refreshed, SavedImages.None);

        using (var db = _database.CreateContext())
        {
            var t = db.Titles.Single();
            Assert.Equal("Sousou no Frieren", t.Name);
            Assert.Equal("New overview from AniList.", t.Overview);
            Assert.Equal("images/titles/anilist-154587-poster.jpg", t.PosterPath);
            Assert.Equal(MetadataSource.User, db.FieldSources.Single(s => s.FieldName == TitleFields.Name).Source);

            var watch = db.Watches.Include(w => w.Episode).Single();
            Assert.Equal((5.0, "Lovely", 1), (watch.Rating!.Value, watch.Note, watch.Episode!.Number));
            Assert.Equal(3, db.Episodes.Count());
            Assert.Equal(2, db.People.Count());
        }
    }

    [Fact]
    public void Tmdb_movie_and_tv_with_the_same_id_are_different_titles()
    {
        _store.Save(new FetchedTitle { Kind = TitleKind.Movie, Format = TitleFormat.Movie, TmdbType = TmdbType.Movie, TmdbId = 100, Name = "A movie" }.WithSource(MetadataSource.Tmdb), SavedImages.None);
        _store.Save(new FetchedTitle { Kind = TitleKind.Tv, Format = TitleFormat.Series, TmdbType = TmdbType.Tv, TmdbId = 100, Name = "A show" }.WithSource(MetadataSource.Tmdb), SavedImages.None);

        Assert.Single(_store.List(TitleKind.Movie));
        Assert.Single(_store.List(TitleKind.Tv));
    }

    [Fact]
    public void The_same_person_from_both_sources_becomes_one_person()
    {
        _store.Save(Frieren(), Images);
        _store.Save(new FetchedTitle
        {
            Kind = TitleKind.Anime, Format = TitleFormat.Film, TmdbType = TmdbType.Movie, TmdbId = 5, Name = "Another film",
            Credits = [new() { Role = CreditRole.Voice, Person = new() { Name = "Atsumi Tanezaki", TmdbId = 77 } }],
        }.WithSource(MetadataSource.Tmdb), SavedImages.None);

        using var db = _database.CreateContext();
        var person = db.People.Single(p => p.Name == "Atsumi Tanezaki");
        Assert.Equal((2, 77), (person.AniListId!.Value, person.TmdbId!.Value));
        Assert.Equal(2, db.Credits.Count(c => c.PersonId == person.Id));
    }

    [Fact]
    public void Nothing_is_preloaded_after_saving_one_title()
    {
        _store.Save(Frieren(), Images);

        using var db = _database.CreateContext();
        Assert.Equal(0, db.Theaters.Count());
        Assert.Equal(0, db.Companions.Count());
        Assert.Equal(0, db.Watches.Count());
        Assert.Equal(0, db.Watchlist.Count());
    }
}
