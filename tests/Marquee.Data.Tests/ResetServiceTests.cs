using Marquee.Core.Model;
using Marquee.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Marquee.Data.Tests;

public sealed class ResetServiceTests : IDisposable
{
    private readonly MarqueeFolder _folder =
        MarqueeFolder.AtRoot(Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N")));
    private readonly MarqueeDatabase _database;
    private readonly SettingsStore _settings;

    public ResetServiceTests()
    {
        _database = new MarqueeDatabase(_folder);
        _database.Initialize();
        _settings = new SettingsStore(_database);
        _settings.Set(SettingsStore.SetupCompleted, "true");
        _settings.Set(SettingsStore.TmdbApiKey, "abc");

        using (var db = _database.CreateContext())
        {
            var title = new Title { Name = "Heat", Kind = TitleKind.Movie, Format = TitleFormat.Movie };
            var theater = new Theater { Name = "The Grand Palace" };
            var sam = new Companion { Name = "Sam" };
            var person = new Person { Name = "Michael Mann" };
            var watch = new Watch { TitleId = title.Id, TheaterId = theater.Id, Where = WatchPlace.Theater, WatchedOn = new DateOnly(2026, 10, 24) };
            db.AddRange(title, theater, sam, person, watch,
                new WatchCompanion { WatchId = watch.Id, CompanionId = sam.Id },
                new Credit { TitleId = title.Id, PersonId = person.Id, Role = CreditRole.Director });
            db.SaveChanges();
        }
        Directory.CreateDirectory(Path.Combine(_folder.Images, "titles"));
        File.WriteAllBytes(Path.Combine(_folder.Images, "titles", "heat.jpg"), [1, 2, 3]);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder.Root))
            Directory.Delete(_folder.Root, recursive: true);
    }

    private int Count(Func<MarqueeDbContext, int> count)
    {
        using var db = _database.CreateContext();
        return count(db);
    }

    [Fact]
    public void Erase_removes_all_data_and_images_but_keeps_settings()
    {
        var backup = new ResetService(_database).EraseAll(backupFirst: false, includeSettings: false);

        Assert.Null(backup);
        Assert.Equal(0, Count(db => db.Titles.Count() + db.Watches.Count() + db.Theaters.Count()
            + db.Companions.Count() + db.People.Count() + db.Credits.Count() + db.WatchCompanions.Count()));
        Assert.Empty(Directory.GetFiles(_folder.Images, "*", SearchOption.AllDirectories));
        Assert.True(Directory.Exists(_folder.Images));
        Assert.True(_settings.IsSetupCompleted);
        Assert.Equal("abc", _settings.Get(SettingsStore.TmdbApiKey));
    }

    [Fact]
    public void Erasing_settings_too_brings_back_the_setup_screen()
    {
        new ResetService(_database).EraseAll(backupFirst: false, includeSettings: true);

        Assert.False(_settings.IsSetupCompleted);
        Assert.Null(_settings.Get(SettingsStore.TmdbApiKey));
    }

    [Fact]
    public void The_backup_is_made_first_inside_the_Marquee_folder_and_holds_the_old_diary()
    {
        var backup = new ResetService(_database).EraseAll(backupFirst: true, includeSettings: false);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
        Assert.StartsWith(_folder.Backups, backup);
        using var copy = new SqliteConnection($"Data Source={backup};Pooling=False");
        copy.Open();
        using var cmd = copy.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Titles";
        Assert.Equal("Heat", cmd.ExecuteScalar());
        Assert.Equal(0, Count(db => db.Titles.Count()));
    }

    [Fact]
    public void The_diary_still_works_after_erasing()
    {
        new ResetService(_database).EraseAll(backupFirst: false, includeSettings: false);

        var theater = new TheaterStore(_database).Save(null, "Rialto", null, null, []);
        Assert.Single(new TheaterStore(_database).List());
        Assert.NotNull(theater.Id);
    }
}
