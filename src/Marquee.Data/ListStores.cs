using Marquee.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data;

/// <summary>Titles you want to watch. Coming soon and out now are worked out from the release date when shown.</summary>
public sealed class WatchlistStore(MarqueeDatabase database)
{
    /// <summary>Adds a title (or updates its expected date). Adding twice keeps one entry.</summary>
    public void Add(string titleId, DateOnly addedOn, DateOnly? expectedRelease = null)
    {
        using var db = database.CreateContext();
        var item = db.Watchlist.SingleOrDefault(w => w.TitleId == titleId);
        if (item is null)
            db.Watchlist.Add(new WatchlistItem { TitleId = titleId, AddedOn = addedOn, ExpectedRelease = expectedRelease });
        else if (expectedRelease is not null)
            item.ExpectedRelease = expectedRelease;
        db.SaveChanges();
    }

    public void Remove(string titleId)
    {
        using var db = database.CreateContext();
        db.Watchlist.Where(w => w.TitleId == titleId).ExecuteDelete();
    }

    public bool Contains(string titleId)
    {
        using var db = database.CreateContext();
        return db.Watchlist.Any(w => w.TitleId == titleId);
    }

    /// <summary>Every watchlist entry with its title and seasons, newest first.</summary>
    public List<WatchlistItem> List()
    {
        using var db = database.CreateContext();
        return db.Watchlist.AsNoTracking().AsSplitQuery()
            .Include(w => w.Title).ThenInclude(t => t!.Seasons).ThenInclude(s => s.Episodes)
            .ToList()
            .OrderByDescending(w => w.AddedOn)
            .ThenBy(w => w.Title?.Name)
            .ToList();
    }

    /// <summary>Ids of titles on the watchlist, for marking search results.</summary>
    public HashSet<string> TitleIds()
    {
        using var db = database.CreateContext();
        return db.Watchlist.Select(w => w.TitleId).ToHashSet();
    }
}

/// <summary>Shows you paused or dropped. On Ice and Watching never overlap.</summary>
public sealed class IceStore(MarqueeDatabase database)
{
    /// <summary>Puts a show on ice, or updates it if it already is.</summary>
    public void Put(string titleId, IceState state, int? stoppedAtSeason, int? stoppedAtEpisode, string? reason, string? note, DateOnly icedOn)
    {
        using var db = database.CreateContext();
        var item = db.OnIce.SingleOrDefault(i => i.TitleId == titleId);
        if (item is null)
        {
            item = new IceItem { TitleId = titleId };
            db.OnIce.Add(item);
        }
        item.State = state;
        item.StoppedAtSeason = stoppedAtSeason;
        item.StoppedAtEpisode = stoppedAtEpisode;
        item.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        item.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        item.IcedOn = icedOn;
        db.SaveChanges();
    }

    public void SetState(string titleId, IceState state)
    {
        using var db = database.CreateContext();
        db.OnIce.Where(i => i.TitleId == titleId).ExecuteUpdate(u => u.SetProperty(i => i.State, state));
    }

    /// <summary>Takes a show off ice. Its diary entries were never touched, so it is back to Watching.</summary>
    public void Resume(string titleId)
    {
        using var db = database.CreateContext();
        db.OnIce.Where(i => i.TitleId == titleId).ExecuteDelete();
    }

    public IceItem? Get(string titleId)
    {
        using var db = database.CreateContext();
        return db.OnIce.AsNoTracking().SingleOrDefault(i => i.TitleId == titleId);
    }

    /// <summary>Every show on ice with its seasons, most recently iced first.</summary>
    public List<IceItem> List()
    {
        using var db = database.CreateContext();
        return db.OnIce.AsNoTracking().AsSplitQuery()
            .Include(i => i.Title).ThenInclude(t => t!.Seasons).ThenInclude(s => s.Episodes)
            .ToList()
            .OrderByDescending(i => i.IcedOn)
            .ToList();
    }
}
