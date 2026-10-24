using Marquee.Core.Model;
using Marquee.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Marquee.Data.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly MarqueeFolder _folder =
        MarqueeFolder.AtRoot(Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder.Root))
            Directory.Delete(_folder.Root, recursive: true);
    }

    [Fact]
    public void Initialize_creates_the_diary_inside_the_Marquee_folder()
    {
        new MarqueeDatabase(_folder).Initialize();

        Assert.True(File.Exists(_folder.Database));
        SqliteConnection.ClearAllPools();
        var everything = Directory.GetFileSystemEntries(_folder.Root, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(everything);
        Assert.All(everything, path => Assert.True(_folder.Contains(path), $"{path} is outside the Marquee folder"));
    }

    [Fact]
    public void First_migration_creates_every_table_and_nothing_is_preloaded()
    {
        var database = new MarqueeDatabase(_folder);
        database.Initialize();

        using var db = database.CreateContext();
        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.Equal(0, db.Titles.Count());
        Assert.Equal(0, db.People.Count());
        Assert.Equal(0, db.Theaters.Count());
        Assert.Equal(0, db.Companions.Count());
        Assert.Equal(0, db.Genres.Count());
        Assert.Equal(0, db.Watches.Count());
        Assert.Equal(0, db.Settings.Count());
    }

    [Fact]
    public void Initialize_twice_is_safe()
    {
        var database = new MarqueeDatabase(_folder);
        database.Initialize();
        database.Initialize();

        using var db = database.CreateContext();
        Assert.Empty(db.Database.GetPendingMigrations());
    }

    [Fact]
    public void Enums_are_stored_as_lowercase_text()
    {
        var database = new MarqueeDatabase(_folder);
        database.Initialize();

        using (var db = database.CreateContext())
        {
            db.Titles.Add(new Title { Name = "Test", Kind = TitleKind.Anime, Format = TitleFormat.Ova });
            db.SaveChanges();
        }

        using var raw = new SqliteConnection($"Data Source={_folder.Database}");
        raw.Open();
        using var cmd = raw.CreateCommand();
        cmd.CommandText = "SELECT Kind || '/' || Format FROM Titles";
        Assert.Equal("anime/ova", cmd.ExecuteScalar());
    }

    [Fact]
    public void Old_streaming_watches_become_home_and_keep_their_service()
    {
        var database = new MarqueeDatabase(_folder);
        _folder.EnsureCreated();
        using (var db = database.CreateContext())
            db.GetInfrastructure().GetRequiredService<IMigrator>().Migrate("20261024212707_AddTmdbTypeAndPersonIndexes");

        using (var raw = new SqliteConnection($"Data Source={_folder.Database}"))
        {
            raw.Open();
            using var cmd = raw.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Titles (Id, Kind, Format, Name) VALUES ('t1', 'movie', 'movie', 'Heat');
                INSERT INTO Watches (Id, TitleId, WatchedOn, IsRewatch, "Where", Service) VALUES ('w1', 't1', '2026-10-24', 0, 'streaming', 'Netflix');
                """;
            cmd.ExecuteNonQuery();
        }

        database.Initialize();

        using var after = database.CreateContext();
        var watch = after.Watches.Single();
        Assert.Equal((WatchPlace.Home, "Netflix"), (watch.Where, watch.Service));
    }

    [Fact]
    public void Ids_are_guid_strings()
    {
        var title = new Title { Name = "Test" };

        Assert.True(Guid.TryParse(title.Id, out _));
    }

    [Fact]
    public void Deleting_a_theater_keeps_the_watch()
    {
        var database = new MarqueeDatabase(_folder);
        database.Initialize();
        var title = new Title { Name = "Test", Kind = TitleKind.Movie, Format = TitleFormat.Movie };
        var theater = new Theater { Name = "My theater", Formats = ["IMAX", "70mm"] };
        var watch = new Watch { TitleId = title.Id, TheaterId = theater.Id, Where = WatchPlace.Theater, WatchedOn = new DateOnly(2026, 10, 24) };

        using (var db = database.CreateContext())
        {
            db.AddRange(title, theater, watch);
            db.SaveChanges();
        }
        using (var db = database.CreateContext())
        {
            Assert.Equal(["IMAX", "70mm"], db.Theaters.Single().Formats);
            db.Theaters.Remove(db.Theaters.Single());
            db.SaveChanges();
        }
        using (var db = database.CreateContext())
        {
            var kept = db.Watches.Single();
            Assert.Null(kept.TheaterId);
        }
    }

    [Fact]
    public void Settings_round_trip()
    {
        var database = new MarqueeDatabase(_folder);
        database.Initialize();
        var settings = new SettingsStore(database);

        Assert.False(settings.IsSetupCompleted);
        settings.Set(SettingsStore.TmdbApiKey, "abc");
        settings.Set(SettingsStore.SetupCompleted, "true");
        settings.Set(SettingsStore.TmdbApiKey, "def");

        Assert.True(settings.IsSetupCompleted);
        Assert.Equal("def", settings.Get(SettingsStore.TmdbApiKey));
        Assert.Null(settings.Get("missing"));
    }
}
