using Marquee.Core.Model;
using Marquee.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data;

/// <summary>What the Log a watch dialog saves.</summary>
public sealed record WatchInput
{
    public required string TitleId { get; init; }
    public string? EpisodeId { get; init; }
    public required DateOnly WatchedOn { get; init; }
    public TimeOnly? StartTime { get; init; }
    public double? Rating { get; init; }
    public bool IsRewatch { get; init; }
    public WatchPlace Where { get; init; }
    public string? TheaterId { get; init; }
    public string? Screen { get; init; }
    public string? Seat { get; init; }
    public string? Format { get; init; }
    public string? Service { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<string> CompanionIds { get; init; } = [];
}

/// <summary>Diary entries. Every watch is its own row, so rewatches count.</summary>
public sealed class WatchStore(MarqueeDatabase database)
{
    public Watch Add(WatchInput input)
    {
        using var db = database.CreateContext();
        var watch = new Watch();
        Apply(db, watch, input);
        db.Watches.Add(watch);
        db.SaveChanges();
        return watch;
    }

    public void Update(string watchId, WatchInput input)
    {
        using var db = database.CreateContext();
        var watch = db.Watches.Include(w => w.Companions).Single(w => w.Id == watchId);
        Apply(db, watch, input);
        db.SaveChanges();
    }

    public void Delete(string watchId)
    {
        using var db = database.CreateContext();
        db.WatchCompanions.RemoveRange(db.WatchCompanions.Where(c => c.WatchId == watchId));
        db.Watches.Remove(db.Watches.Single(w => w.Id == watchId));
        db.SaveChanges();
    }

    /// <summary>One watch with everything the dialog and diary show.</summary>
    public Watch? Get(string watchId)
    {
        using var db = database.CreateContext();
        return Full(db).FirstOrDefault(w => w.Id == watchId);
    }

    /// <summary>Every watch in one year, newest first.</summary>
    public List<Watch> ListYear(int year)
    {
        using var db = database.CreateContext();
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        return Newest(Full(db).Where(w => w.WatchedOn >= from && w.WatchedOn <= to).ToList());
    }

    /// <summary>Years that have at least one watch, newest first.</summary>
    public List<int> Years()
    {
        using var db = database.CreateContext();
        return db.Watches.Select(w => w.WatchedOn).ToList().Select(d => d.Year).Distinct().OrderDescending().ToList();
    }

    public int Count()
    {
        using var db = database.CreateContext();
        return db.Watches.Count();
    }

    /// <summary>
    /// The first time this title (or this episode) was watched, ignoring one watch (the one
    /// being edited). Used to suggest "Rewatch" and to show "First seen".
    /// </summary>
    public DateOnly? FirstSeen(string titleId, string? episodeId, string? exceptWatchId = null)
    {
        using var db = database.CreateContext();
        var query = db.Watches.Where(w => w.TitleId == titleId && w.Id != exceptWatchId);
        query = episodeId is null ? query : query.Where(w => w.EpisodeId == episodeId);
        return query.Select(w => (DateOnly?)w.WatchedOn).ToList().Min();
    }

    /// <summary>Ids of the episodes of a title that have been watched at least once.</summary>
    public HashSet<string> WatchedEpisodeIds(string titleId)
    {
        using var db = database.CreateContext();
        return db.Watches.Where(w => w.TitleId == titleId && w.EpisodeId != null).Select(w => w.EpisodeId!).ToHashSet();
    }

    /// <summary>Services used before, most used first, for suggestions in the dialog.</summary>
    public List<string> KnownServices()
    {
        using var db = database.CreateContext();
        return db.Watches.Where(w => w.Service != null).Select(w => w.Service!).ToList()
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.First())
            .ToList();
    }

    internal static IQueryable<Watch> Full(MarqueeDbContext db) => db.Watches
        .AsNoTracking()
        .Include(w => w.Title)
        .Include(w => w.Episode).ThenInclude(e => e!.Season)
        .Include(w => w.Theater)
        .Include(w => w.Companions).ThenInclude(c => c.Companion);

    /// <summary>Newest first. Same-day episodes show the latest episode on top.</summary>
    internal static List<Watch> Newest(IEnumerable<Watch> watches) => watches
        .OrderByDescending(w => w.WatchedOn)
        .ThenByDescending(w => w.StartTime ?? TimeOnly.MinValue)
        .ThenByDescending(w => w.Episode?.Season?.Number ?? 0)
        .ThenByDescending(w => w.Episode?.Number ?? 0)
        .ToList();

    private static void Apply(MarqueeDbContext db, Watch watch, WatchInput input)
    {
        if (!Rating.IsValid(input.Rating))
            throw new ArgumentException("Ratings run from 0.5 to 5 in half steps.");

        var atTheater = input.Where == WatchPlace.Theater;
        watch.TitleId = input.TitleId;
        watch.EpisodeId = input.EpisodeId;
        watch.WatchedOn = input.WatchedOn;
        watch.StartTime = input.StartTime;
        watch.Rating = input.Rating;
        watch.IsRewatch = input.IsRewatch;
        watch.Where = input.Where;
        // Theater details only belong to theater visits; a service only to the other places.
        watch.TheaterId = atTheater ? input.TheaterId : null;
        watch.Screen = atTheater ? Clean(input.Screen) : null;
        watch.Seat = atTheater ? Clean(input.Seat) : null;
        watch.Format = atTheater ? Clean(input.Format) : null;
        watch.Service = atTheater ? null : Clean(input.Service);
        watch.Note = Clean(input.Note);

        // Keep links that stay, drop removed ones, add new ones.
        var wanted = input.CompanionIds.ToHashSet();
        foreach (var link in watch.Companions.Where(c => !wanted.Contains(c.CompanionId)).ToList())
        {
            watch.Companions.Remove(link);
            db.WatchCompanions.Remove(link);
        }
        foreach (var id in wanted.Where(id => watch.Companions.All(c => c.CompanionId != id)))
            watch.Companions.Add(new WatchCompanion { WatchId = watch.Id, CompanionId = id });
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
