using Marquee.Core.Model;
using Marquee.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Marquee.Data.Tests;

public sealed class ListStoreTests : IDisposable
{
    private readonly MarqueeFolder _folder =
        MarqueeFolder.AtRoot(Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N")));
    private readonly MarqueeDatabase _database;
    private readonly WatchlistStore _watchlist;
    private readonly IceStore _ice;
    private readonly WatchStore _watches;
    private readonly string _movie;
    private readonly string _show;
    private readonly string _episode;

    public ListStoreTests()
    {
        _database = new MarqueeDatabase(_folder);
        _database.Initialize();
        _watchlist = new WatchlistStore(_database);
        _ice = new IceStore(_database);
        _watches = new WatchStore(_database);

        using var db = _database.CreateContext();
        var movie = new Title { Name = "The Salt Meridian", Kind = TitleKind.Movie, Format = TitleFormat.Movie, ReleaseDate = new DateOnly(2026, 11, 14) };
        var show = new Title { Name = "The Wire", Kind = TitleKind.Tv, Format = TitleFormat.Series };
        var season = new Season { TitleId = show.Id, Number = 1 };
        var episode = new Episode { SeasonId = season.Id, Number = 1 };
        db.AddRange(movie, show, season, episode);
        db.SaveChanges();
        (_movie, _show, _episode) = (movie.Id, show.Id, episode.Id);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder.Root))
            Directory.Delete(_folder.Root, recursive: true);
    }

    [Fact]
    public void Adding_twice_keeps_one_entry_and_remove_takes_it_off()
    {
        _watchlist.Add(_movie, new DateOnly(2026, 10, 1));
        _watchlist.Add(_movie, new DateOnly(2026, 10, 2), expectedRelease: new DateOnly(2026, 12, 1));

        var item = Assert.Single(_watchlist.List());
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 1)), (item.AddedOn, item.ExpectedRelease!.Value));
        Assert.Equal("The Salt Meridian", item.Title!.Name);
        Assert.True(_watchlist.Contains(_movie));

        _watchlist.Remove(_movie);
        Assert.Empty(_watchlist.List());
    }

    [Fact]
    public void Watching_a_title_takes_it_off_the_watchlist()
    {
        _watchlist.Add(_movie, new DateOnly(2026, 10, 1));
        _watchlist.Add(_show, new DateOnly(2026, 10, 1));

        _watches.Add(new WatchInput { TitleId = _movie, WatchedOn = new DateOnly(2026, 11, 20) });
        _watches.MarkEpisodesWatched(_show, [_episode], new DateOnly(2026, 11, 21));

        Assert.Empty(_watchlist.List());
    }

    [Fact]
    public void Ice_keeps_where_you_stopped_and_can_switch_state_or_resume()
    {
        _ice.Put(_show, IceState.Paused, 2, 3, "Needed a break", "Back in winter.", new DateOnly(2026, 8, 14));
        _ice.Put(_show, IceState.Paused, 2, 3, "  ", null, new DateOnly(2026, 8, 15));

        var item = Assert.Single(_ice.List());
        Assert.Equal((IceState.Paused, 2, 3, (string?)null, new DateOnly(2026, 8, 15)), (item.State, item.StoppedAtSeason!.Value, item.StoppedAtEpisode!.Value, item.Reason, item.IcedOn));

        _ice.SetState(_show, IceState.Dropped);
        Assert.Equal(IceState.Dropped, _ice.Get(_show)!.State);

        _ice.Resume(_show);
        Assert.Null(_ice.Get(_show));
    }

    [Fact]
    public void Logging_an_episode_of_a_show_on_ice_resumes_it_and_keeps_history()
    {
        _watches.Add(new WatchInput { TitleId = _show, EpisodeId = _episode, WatchedOn = new DateOnly(2026, 6, 1) });
        _ice.Put(_show, IceState.Dropped, 1, 1, "Lost interest", null, new DateOnly(2026, 7, 1));

        _watches.Add(new WatchInput { TitleId = _show, EpisodeId = _episode, WatchedOn = new DateOnly(2026, 10, 24), IsRewatch = true });

        Assert.Null(_ice.Get(_show));
        Assert.Equal(2, _watches.ListForTitle(_show).Count);
    }

    [Fact]
    public void Erasing_all_data_clears_the_watchlist_and_ice()
    {
        _watchlist.Add(_movie, new DateOnly(2026, 10, 1));
        _ice.Put(_show, IceState.Paused, null, null, null, null, new DateOnly(2026, 10, 1));

        new ResetService(_database).EraseAll(backupFirst: false, includeSettings: false);

        Assert.Empty(_watchlist.List());
        Assert.Empty(_ice.List());
    }
}
