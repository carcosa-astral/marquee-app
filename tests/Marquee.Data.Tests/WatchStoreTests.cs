using Marquee.Core.Model;
using Marquee.Core.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data.Tests;

public sealed class WatchStoreTests : IDisposable
{
    private readonly MarqueeFolder _folder =
        MarqueeFolder.AtRoot(Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N")));
    private readonly MarqueeDatabase _database;
    private readonly WatchStore _watches;
    private readonly TheaterStore _theaters;
    private readonly CompanionStore _companions;
    private readonly string _heat;
    private readonly string _episode1;
    private readonly string _episode2;
    private readonly string _show;

    public WatchStoreTests()
    {
        _database = new MarqueeDatabase(_folder);
        _database.Initialize();
        _watches = new WatchStore(_database);
        _theaters = new TheaterStore(_database);
        _companions = new CompanionStore(_database);

        using var db = _database.CreateContext();
        var heat = new Title { Name = "Heat", Kind = TitleKind.Movie, Format = TitleFormat.Movie, Year = 1995 };
        var show = new Title { Name = "Frieren", Kind = TitleKind.Anime, Format = TitleFormat.Series };
        var season = new Season { TitleId = show.Id, Number = 1 };
        var e1 = new Episode { SeasonId = season.Id, Number = 1 };
        var e2 = new Episode { SeasonId = season.Id, Number = 2 };
        db.AddRange(heat, show, season, e1, e2);
        db.SaveChanges();
        (_heat, _show, _episode1, _episode2) = (heat.Id, show.Id, e1.Id, e2.Id);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder.Root))
            Directory.Delete(_folder.Root, recursive: true);
    }

    private WatchInput Heat(DateOnly on, WatchPlace where = WatchPlace.Home, double? rating = null, string? theaterId = null, params string[] companions) => new()
    {
        TitleId = _heat, WatchedOn = on, Where = where, Rating = rating, TheaterId = theaterId,
        Screen = "4", Seat = "H12", Format = "35mm", Service = "Plex", Note = "  Great.  ",
        CompanionIds = companions,
    };

    [Fact]
    public void A_watch_at_home_keeps_the_service_and_drops_theater_details()
    {
        var w = _watches.Add(Heat(new DateOnly(2026, 10, 24)));

        var saved = _watches.Get(w.Id)!;
        Assert.Equal(("Plex", "Great."), (saved.Service, saved.Note));
        Assert.Null(saved.Screen);
        Assert.Null(saved.Format);
        Assert.Null(saved.TheaterId);
    }

    [Fact]
    public void A_theater_visit_keeps_theater_details_and_drops_the_service()
    {
        var theater = _theaters.Save(null, "The Grand Palace", "Springfield", null, ["35mm", "IMAX"]);
        var w = _watches.Add(Heat(new DateOnly(2026, 10, 24), WatchPlace.Theater, 4.5, theater.Id));

        var saved = _watches.Get(w.Id)!;
        Assert.Equal(("The Grand Palace", "4", "H12", "35mm"), (saved.Theater!.Name, saved.Screen, saved.Seat, saved.Format));
        Assert.Null(saved.Service);
    }

    [Fact]
    public void Rewatches_are_separate_entries_and_first_seen_is_the_earliest()
    {
        _watches.Add(Heat(new DateOnly(2024, 3, 2)));
        var second = _watches.Add(Heat(new DateOnly(2026, 10, 24)) with { IsRewatch = true });

        Assert.Equal(2, _watches.Count());
        Assert.Equal(new DateOnly(2024, 3, 2), _watches.FirstSeen(_heat, null));
        Assert.Equal(new DateOnly(2024, 3, 2), _watches.FirstSeen(_heat, null, exceptWatchId: second.Id));
        Assert.Equal([2026, 2024], _watches.Years());
        Assert.Single(_watches.ListYear(2026));
    }

    [Fact]
    public void Invalid_ratings_are_refused()
    {
        Assert.Throws<ArgumentException>(() => _watches.Add(Heat(new DateOnly(2026, 1, 1), rating: 4.3)));
        Assert.Equal(0, _watches.Count());
    }

    [Fact]
    public void Editing_changes_companions_without_duplicates()
    {
        var sam = _companions.FindOrCreate("Sam");
        var alex = _companions.FindOrCreate("Alex");
        var w = _watches.Add(Heat(new DateOnly(2026, 10, 24), companions: [sam.Id]));

        _watches.Update(w.Id, Heat(new DateOnly(2026, 10, 25), companions: [sam.Id, alex.Id]));
        _watches.Update(w.Id, Heat(new DateOnly(2026, 10, 25), companions: [alex.Id]));

        var saved = _watches.Get(w.Id)!;
        Assert.Equal(new DateOnly(2026, 10, 25), saved.WatchedOn);
        Assert.Equal(["Alex"], saved.Companions.Select(c => c.Companion!.Name));
        using var db = _database.CreateContext();
        Assert.Equal(1, db.WatchCompanions.Count());
    }

    [Fact]
    public void Deleting_a_watch_removes_only_that_entry()
    {
        var sam = _companions.FindOrCreate("Sam");
        var keep = _watches.Add(Heat(new DateOnly(2026, 1, 1)));
        var gone = _watches.Add(Heat(new DateOnly(2026, 2, 1), companions: [sam.Id]));

        _watches.Delete(gone.Id);

        Assert.NotNull(_watches.Get(keep.Id));
        Assert.Null(_watches.Get(gone.Id));
        Assert.Single(_companions.List());
    }

    [Fact]
    public void Episodes_are_tracked_and_ordered_newest_first_on_the_same_day()
    {
        var day = new DateOnly(2026, 10, 21);
        _watches.Add(new WatchInput { TitleId = _show, EpisodeId = _episode1, WatchedOn = day });
        _watches.Add(new WatchInput { TitleId = _show, EpisodeId = _episode2, WatchedOn = day });

        Assert.Equal([_episode2, _episode1], _watches.ListYear(2026).Select(w => w.EpisodeId));
        Assert.Equal(new HashSet<string> { _episode1, _episode2 }, _watches.WatchedEpisodeIds(_show));
        Assert.Equal(day, _watches.FirstSeen(_show, _episode2));
    }

    [Fact]
    public void Theater_stats_count_visits_and_average_ratings()
    {
        var palace = _theaters.Save(null, "The Grand Palace", "Springfield", null, ["35mm"]);
        _theaters.Save(null, "Rialto", null, "Starlight", []);
        _watches.Add(Heat(new DateOnly(2025, 7, 1), WatchPlace.Theater, 4.0, palace.Id));
        _watches.Add(Heat(new DateOnly(2026, 10, 24), WatchPlace.Theater, 5.0, palace.Id));
        _watches.Add(Heat(new DateOnly(2026, 10, 25), WatchPlace.Theater, null, palace.Id));

        var stats = _theaters.ListWithStats();

        Assert.Equal(["The Grand Palace", "Rialto"], stats.Select(s => s.Theater.Name));
        var p = stats[0];
        Assert.Equal((3, 4.5), (p.Visits, p.AverageRating!.Value));
        Assert.Equal((new DateOnly(2025, 7, 1), new DateOnly(2026, 10, 25)), (p.FirstVisit!.Value, p.LastVisit!.Value));
        Assert.Equal(0, stats[1].Visits);
        Assert.Null(stats[1].AverageRating);
        Assert.Equal(new DateOnly(2026, 10, 25), _theaters.RecentVisits(palace.Id, 5)[0].WatchedOn);
        Assert.Equal(3, _theaters.TotalVisits());
    }

    [Fact]
    public void Typing_an_existing_name_reuses_it_ignoring_case()
    {
        var a = _theaters.FindOrCreate("Rialto Repertory");
        var b = _theaters.FindOrCreate("  rialto repertory ");
        var c = _companions.FindOrCreate("Sam");
        var d = _companions.FindOrCreate("sam");

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(c.Id, d.Id);
        Assert.Single(_theaters.List());
        Assert.Single(_companions.List());
    }

    [Fact]
    public void Editing_a_theater_updates_it_in_place()
    {
        var t = _theaters.Save(null, "Rialto", null, null, ["Standard"]);

        _theaters.Save(t.Id, "Rialto Repertory", "Springfield", " ", ["Standard", "70mm", "standard"]);

        var saved = Assert.Single(_theaters.List());
        Assert.Equal(("Rialto Repertory", "Springfield", (string?)null), (saved.Name, saved.City, saved.Chain));
        Assert.Equal(["Standard", "70mm"], saved.Formats);
        Assert.Throws<ArgumentException>(() => _theaters.Save(null, "  ", null, null, []));
    }
}
