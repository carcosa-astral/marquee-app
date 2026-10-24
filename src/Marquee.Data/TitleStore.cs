using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data;

/// <summary>Saves fetched titles and reads the library.</summary>
public sealed class TitleStore(MarqueeDatabase database)
{
    /// <summary>The ids already in the library, so search can mark results as added.</summary>
    public LibraryIds GetLibraryIds()
    {
        using var db = database.CreateContext();
        var rows = db.Titles.Select(t => new { t.TmdbType, t.TmdbId, t.AniListId }).ToList();
        return new LibraryIds(
            rows.Where(r => r.TmdbId is not null && r.TmdbType is not null).Select(r => (r.TmdbType!.Value, r.TmdbId!.Value)).ToHashSet(),
            rows.Where(r => r.AniListId is not null).Select(r => r.AniListId!.Value).ToHashSet());
    }

    /// <summary>The saved title for a search result, if it is already in the library.</summary>
    public string? FindId(SearchResult result)
    {
        using var db = database.CreateContext();
        if (result.AniList is { } al && db.Titles.FirstOrDefault(t => t.AniListId == al.Id) is { } byAniList)
            return byAniList.Id;
        if (result.Tmdb is { TmdbType: { } type } tmdb && db.Titles.FirstOrDefault(t => t.TmdbId == tmdb.Id && t.TmdbType == type) is { } byTmdb)
            return byTmdb.Id;
        return null;
    }

    /// <summary>Every title in the library, for picking one in the Log a watch dialog.</summary>
    public List<Title> ListAll()
    {
        using var db = database.CreateContext();
        return db.Titles.AsNoTracking().ToList().OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>A title with its seasons and episodes in order.</summary>
    public Title? GetWithEpisodes(string titleId)
    {
        using var db = database.CreateContext();
        var title = db.Titles.AsNoTracking().Include(t => t.Seasons).ThenInclude(s => s.Episodes).FirstOrDefault(t => t.Id == titleId);
        if (title is null)
            return null;
        title.Seasons = title.Seasons.OrderBy(s => s.Number).ToList();
        foreach (var season in title.Seasons)
            season.Episodes = season.Episodes.OrderBy(e => e.Number).ToList();
        return title;
    }

    public List<Title> List(TitleKind kind)
    {
        using var db = database.CreateContext();
        return db.Titles.AsNoTracking().Where(t => t.Kind == kind).OrderBy(t => t.Name).ToList();
    }

    /// <summary>
    /// Adds a new title or refreshes the one that is already saved.
    /// Fields the user changed by hand are left alone, and watches, watchlist,
    /// On Ice and other user data are never touched.
    /// </summary>
    public Title Save(FetchedTitle fetched, SavedImages images)
    {
        using var db = database.CreateContext();
        using var tx = db.Database.BeginTransaction();

        var title = FindExisting(db, fetched);
        if (title is null)
        {
            title = new Title();
            db.Titles.Add(title);
        }

        title.Kind = fetched.Kind;
        title.Format = fetched.Format;
        title.TmdbType = fetched.TmdbType ?? title.TmdbType;
        title.TmdbId = fetched.TmdbId ?? title.TmdbId;
        title.AniListId = fetched.AniListId ?? title.AniListId;
        title.MalId = fetched.MalId ?? title.MalId;
        title.ImdbId = fetched.ImdbId ?? title.ImdbId;

        var overridden = db.FieldOverrides.Where(o => o.TitleId == title.Id).Select(o => o.FieldName).ToHashSet();
        var sources = db.FieldSources.Where(s => s.TitleId == title.Id).ToDictionary(s => s.FieldName);
        foreach (var field in TitleFields.All)
        {
            if (overridden.Contains(field))
            {
                SetSource(db, sources, title.Id, field, MetadataSource.User);
                continue;
            }
            if (!fetched.Sources.TryGetValue(field, out var source) || !fetched.HasValue(field))
                continue;
            Apply(title, field, fetched, images);
            SetSource(db, sources, title.Id, field, source);
        }
        if (string.IsNullOrWhiteSpace(title.Name))
            title.Name = fetched.Name ?? "Untitled";

        db.SaveChanges();

        SaveGenres(db, title, fetched.Genres);
        SaveCredits(db, title, fetched.Credits, images);
        SaveSeasons(db, title, fetched.Seasons);

        db.SaveChanges();
        tx.Commit();
        return title;
    }

    private static Title? FindExisting(MarqueeDbContext db, FetchedTitle f)
    {
        if (f.AniListId is { } al && db.Titles.FirstOrDefault(t => t.AniListId == al) is { } byAniList)
            return byAniList;
        if (f.TmdbId is { } id && f.TmdbType is { } type
            && db.Titles.FirstOrDefault(t => t.TmdbId == id && t.TmdbType == type) is { } byTmdb)
            return byTmdb;
        if (f.MalId is { } mal && db.Titles.FirstOrDefault(t => t.MalId == mal) is { } byMal)
            return byMal;
        return null;
    }

    private static void Apply(Title title, string field, FetchedTitle f, SavedImages images)
    {
        switch (field)
        {
            case TitleFields.Name: title.Name = f.Name!; break;
            case TitleFields.OriginalName: title.OriginalName = f.OriginalName; break;
            case TitleFields.Year: title.Year = f.Year; break;
            case TitleFields.Runtime: title.RuntimeMinutes = f.Runtime; break;
            case TitleFields.Overview: title.Overview = f.Overview; break;
            case TitleFields.ReleaseDate: title.ReleaseDate = f.ReleaseDate; break;
            case TitleFields.Status: title.Status = f.Status; break;
            // Keep the old image if this download failed.
            case TitleFields.Poster: title.PosterPath = images.PosterPath ?? title.PosterPath; break;
            case TitleFields.Backdrop: title.BackdropPath = images.BackdropPath ?? title.BackdropPath; break;
        }
    }

    private static void SetSource(MarqueeDbContext db, Dictionary<string, FieldSource> existing, string titleId, string field, MetadataSource source)
    {
        if (existing.TryGetValue(field, out var row))
        {
            row.Source = source;
            return;
        }
        var added = new FieldSource { TitleId = titleId, FieldName = field, Source = source };
        db.FieldSources.Add(added);
        existing[field] = added;
    }

    private static void SaveGenres(MarqueeDbContext db, Title title, List<string> names)
    {
        if (names.Count == 0)
            return;
        db.TitleGenres.RemoveRange(db.TitleGenres.Where(tg => tg.TitleId == title.Id));
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var genre = db.Genres.Local.FirstOrDefault(g => g.Name == name)
                ?? db.Genres.FirstOrDefault(g => g.Name == name);
            if (genre is null)
            {
                genre = new Genre { Name = name };
                db.Genres.Add(genre);
            }
            db.TitleGenres.Add(new TitleGenre { TitleId = title.Id, GenreId = genre.Id });
        }
    }

    private static void SaveCredits(MarqueeDbContext db, Title title, List<FetchedCredit> credits, SavedImages images)
    {
        if (credits.Count == 0)
            return;
        db.Credits.RemoveRange(db.Credits.Where(c => c.TitleId == title.Id));
        foreach (var c in credits)
        {
            if (string.IsNullOrWhiteSpace(c.Person.Name))
                continue;
            var person = FindOrAddPerson(db, c.Person, images);
            db.Credits.Add(new Credit
            {
                TitleId = title.Id,
                PersonId = person.Id,
                Role = c.Role,
                Character = c.Character,
                Order = c.Order,
            });
        }
    }

    /// <summary>
    /// Matches people by their TMDB or AniList id. Someone known to only one source is
    /// matched by exact name to a person known only to the other source, so a voice actor
    /// from AniList and the same actor from TMDB become one person.
    /// </summary>
    private static Person FindOrAddPerson(MarqueeDbContext db, FetchedPerson p, SavedImages images)
    {
        var person = (p.TmdbId is { } t ? db.People.Local.FirstOrDefault(x => x.TmdbId == t) ?? db.People.FirstOrDefault(x => x.TmdbId == t) : null)
            ?? (p.AniListId is { } a ? db.People.Local.FirstOrDefault(x => x.AniListId == a) ?? db.People.FirstOrDefault(x => x.AniListId == a) : null);

        if (person is null && (p.TmdbId is null) != (p.AniListId is null))
        {
            var fromTmdb = p.TmdbId is not null;
            person = db.People.Local.FirstOrDefault(Candidate)
                ?? db.People.Where(x => x.Name == p.Name).AsEnumerable().FirstOrDefault(Candidate);

            bool Candidate(Person x) => x.Name == p.Name && (fromTmdb ? x.TmdbId is null && x.AniListId is not null : x.AniListId is null && x.TmdbId is not null);
        }

        if (person is null)
        {
            person = new Person { Name = p.Name };
            db.People.Add(person);
        }

        person.TmdbId ??= p.TmdbId;
        person.AniListId ??= p.AniListId;
        person.BirthDate ??= p.BirthDate;
        if (p.PhotoUrl is not null && images.PersonPhotos.TryGetValue(p.PhotoUrl, out var photo))
            person.PhotoPath ??= photo;
        return person;
    }

    /// <summary>Adds missing seasons and episodes and updates their details. Nothing is deleted, so watches keep their episode.</summary>
    private static void SaveSeasons(MarqueeDbContext db, Title title, List<FetchedSeason> seasons)
    {
        foreach (var fs in seasons)
        {
            var season = db.Seasons.Include(s => s.Episodes).FirstOrDefault(s => s.TitleId == title.Id && s.Number == fs.Number);
            if (season is null)
            {
                season = new Season { TitleId = title.Id, Number = fs.Number };
                db.Seasons.Add(season);
            }
            season.Name = fs.Name ?? season.Name;

            foreach (var fe in fs.Episodes)
            {
                var episode = season.Episodes.FirstOrDefault(e => e.Number == fe.Number);
                if (episode is null)
                {
                    episode = new Episode { SeasonId = season.Id, Number = fe.Number };
                    season.Episodes.Add(episode);
                }
                episode.Name = fe.Name ?? episode.Name;
                episode.AirDate = fe.AirDate ?? episode.AirDate;
                episode.RuntimeMinutes = fe.Runtime ?? episode.RuntimeMinutes;
            }
        }
    }
}

public sealed record LibraryIds(HashSet<(TmdbType Type, int Id)> Tmdb, HashSet<int> AniList)
{
    public bool Contains(SearchResult r) =>
        (r.AniList is { } al && AniList.Contains(al.Id))
        || (r.Tmdb is { TmdbType: { } type } t && Tmdb.Contains((type, t.Id)));
}
