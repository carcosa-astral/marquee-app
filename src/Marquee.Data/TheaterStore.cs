using Marquee.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data;

/// <summary>A theater with its visit numbers, for the Theaters page.</summary>
public sealed record TheaterStats(Theater Theater, int Visits, DateOnly? FirstVisit, DateOnly? LastVisit, double? AverageRating);

/// <summary>The user's own theaters. Marquee ships with none.</summary>
public sealed class TheaterStore(MarqueeDatabase database)
{
    /// <summary>Common formats offered as choices. Theaters can add their own.</summary>
    public static readonly IReadOnlyList<string> CommonFormats = ["Standard", "35mm", "70mm", "IMAX", "Dolby"];

    public List<Theater> List()
    {
        using var db = database.CreateContext();
        return db.Theaters.AsNoTracking().ToList().OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Every theater with visits, first and last visit, and average rating. Most visited first.</summary>
    public List<TheaterStats> ListWithStats()
    {
        using var db = database.CreateContext();
        var visits = db.Watches
            .Where(w => w.TheaterId != null)
            .Select(w => new { w.TheaterId, w.WatchedOn, w.Rating })
            .ToList()
            .GroupBy(w => w.TheaterId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        return db.Theaters.AsNoTracking().ToList()
            .Select(t =>
            {
                var v = visits.GetValueOrDefault(t.Id) ?? [];
                var rated = v.Where(x => x.Rating is not null).Select(x => x.Rating!.Value).ToList();
                return new TheaterStats(
                    t,
                    v.Count,
                    v.Count == 0 ? null : v.Min(x => x.WatchedOn),
                    v.Count == 0 ? null : v.Max(x => x.WatchedOn),
                    rated.Count == 0 ? null : Math.Round(rated.Average(), 1));
            })
            .OrderByDescending(s => s.Visits)
            .ThenBy(s => s.Theater.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public List<Watch> RecentVisits(string theaterId, int count)
    {
        using var db = database.CreateContext();
        return WatchStore.Newest(WatchStore.Full(db).Where(w => w.TheaterId == theaterId).ToList()).Take(count).ToList();
    }

    public int TotalVisits()
    {
        using var db = database.CreateContext();
        return db.Watches.Count(w => w.TheaterId != null);
    }

    public Theater Save(string? theaterId, string name, string? city, string? chain, IEnumerable<string> formats)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A theater needs a name.");

        using var db = database.CreateContext();
        var theater = theaterId is null ? null : db.Theaters.Single(t => t.Id == theaterId);
        if (theater is null)
        {
            theater = new Theater();
            db.Theaters.Add(theater);
        }
        theater.Name = name.Trim();
        theater.City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        theater.Chain = string.IsNullOrWhiteSpace(chain) ? null : chain.Trim();
        theater.Formats = formats.Select(f => f.Trim()).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        db.SaveChanges();
        return theater;
    }

    /// <summary>Finds a theater by name, ignoring case, or creates one. Used when a new name is typed while logging.</summary>
    public Theater FindOrCreate(string name)
    {
        var trimmed = name.Trim();
        var existing = List().FirstOrDefault(t => string.Equals(t.Name, trimmed, StringComparison.CurrentCultureIgnoreCase));
        return existing ?? Save(null, trimmed, null, null, []);
    }
}

/// <summary>People the user watches with. Created by the user, never preloaded.</summary>
public sealed class CompanionStore(MarqueeDatabase database)
{
    public List<Companion> List()
    {
        using var db = database.CreateContext();
        return db.Companions.AsNoTracking().ToList().OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public Companion FindOrCreate(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("A companion needs a name.");
        using var db = database.CreateContext();
        var existing = db.Companions.ToList().FirstOrDefault(c => string.Equals(c.Name, trimmed, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null)
            return existing;
        var companion = new Companion { Name = trimmed };
        db.Companions.Add(companion);
        db.SaveChanges();
        return companion;
    }
}
