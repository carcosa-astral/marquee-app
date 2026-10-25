using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data;

/// <summary>Saves fetched titles and reads the library.</summary>
public sealed class TitleStore(MarqueeDatabase database)
{
    /// <summary>The ids of titles with at least one watch, so search can mark results as already in the diary.</summary>
    public LibraryIds GetLibraryIds()
    {
        using var db = database.CreateContext();
        var rows = db.Titles.Where(t => db.Watches.Any(w => w.TitleId == t.Id))
            .Select(t => new { t.TmdbType, t.TmdbId, t.AniListId }).ToList();
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

    /// <summary>The id of an episode, found by season and episode number.</summary>
    public string? FindEpisodeId(string titleId, int season, int episode)
    {
        using var db = database.CreateContext();
        return db.Episodes
            .Where(e => e.Number == episode && e.Season!.TitleId == titleId && e.Season.Number == season)
            .Select(e => e.Id)
            .FirstOrDefault();
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
        return db.Titles.AsNoTracking()
            .Where(t => t.Kind == kind && db.Watches.Any(w => w.TitleId == t.Id))
            .OrderBy(t => t.Name)
            .ToList();
    }

    /// <summary>
    /// Adds a new title or refreshes the one that is already saved.
    /// Fields the user changed by hand are left alone, and watches, watchlist,
    /// On Ice and other user data are never touched.
    /// </summary>
    /// <param name="rematchTitleId">
    /// Fix match: point this saved title at a different TMDB or AniList entry. Its ids are
    /// replaced, not merged, and its diary entries, ratings and edits stay attached.
    /// </param>
    public Title Save(FetchedTitle fetched, SavedImages images, string? rematchTitleId = null)
    {
        using var db = database.CreateContext();
        using var tx = db.Database.BeginTransaction();

        Title? title;
        if (rematchTitleId is not null)
        {
            title = db.Titles.Single(t => t.Id == rematchTitleId);
            if (FindExisting(db, fetched) is { } other && other.Id != title.Id)
                throw new InvalidOperationException($"\"{other.Name}\" is already in your library with that match.");
            if (title.ImdbId != fetched.ImdbId)
            {
                // The IMDb score belonged to the old match.
                title.ImdbRating = null;
                title.ImdbVotes = null;
            }
            title.TmdbType = fetched.TmdbType;
            title.TmdbId = fetched.TmdbId;
            title.AniListId = fetched.AniListId;
            title.MalId = fetched.MalId;
            title.ImdbId = fetched.ImdbId;
        }
        else
        {
            title = FindExisting(db, fetched);
            if (title is null)
            {
                title = new Title();
                db.Titles.Add(title);
            }
            title.TmdbType = fetched.TmdbType ?? title.TmdbType;
            title.TmdbId = fetched.TmdbId ?? title.TmdbId;
            title.AniListId = fetched.AniListId ?? title.AniListId;
            title.MalId = fetched.MalId ?? title.MalId;
            title.ImdbId = fetched.ImdbId ?? title.ImdbId;
        }

        title.Kind = fetched.Kind;
        title.Format = fetched.Format;
        // Scores are not editable; they follow the sources. A refresh that could not reach a
        // source keeps the old score, while Fix match always takes the new match's scores.
        title.TmdbScore = rematchTitleId is null ? fetched.TmdbScore ?? title.TmdbScore : fetched.TmdbScore;
        title.AniListScore = rematchTitleId is null ? fetched.AniListScore ?? title.AniListScore : fetched.AniListScore;

        var overrides = db.FieldOverrides.Where(o => o.TitleId == title.Id).ToDictionary(o => o.FieldName);
        var sources = db.FieldSources.Where(s => s.TitleId == title.Id).ToDictionary(s => s.FieldName);
        foreach (var field in TitleFields.All)
        {
            var hasValue = fetched.Sources.TryGetValue(field, out var source) && fetched.HasValue(field);
            if (overrides.TryGetValue(field, out var edit))
            {
                // The user's value stays. Remember the source's latest value so Reset can bring it back.
                if (hasValue)
                {
                    var scratch = new Title();
                    Apply(scratch, field, fetched, images);
                    edit.OriginalValue = TitleFieldValues.Get(scratch, field) ?? edit.OriginalValue;
                    edit.OriginalSource = source;
                }
                SetSource(db, sources, title.Id, field, MetadataSource.User);
                continue;
            }
            if (!hasValue)
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
        if (rematchTitleId is not null)
            RemoveUnusedEpisodes(db, title, fetched.Seasons);

        db.SaveChanges();
        tx.Commit();
        return title;
    }

    /// <summary>Everything the title page shows about one title.</summary>
    public TitleDetails? GetDetails(string titleId)
    {
        using var db = database.CreateContext();
        var title = db.Titles.AsNoTracking().AsSplitQuery()
            .Include(t => t.Seasons).ThenInclude(s => s.Episodes)
            .Include(t => t.Credits).ThenInclude(c => c.Person)
            .Include(t => t.Genres).ThenInclude(g => g.Genre)
            .FirstOrDefault(t => t.Id == titleId);
        if (title is null)
            return null;
        title.Seasons = title.Seasons.OrderBy(s => s.Number).ToList();
        foreach (var season in title.Seasons)
            season.Episodes = season.Episodes.OrderBy(e => e.Number).ToList();
        title.Credits = title.Credits.OrderBy(c => c.Role).ThenBy(c => c.Order).ToList();

        var sources = db.FieldSources.Where(s => s.TitleId == titleId).ToDictionary(s => s.FieldName, s => s.Source);
        var overrides = db.FieldOverrides.AsNoTracking().Where(o => o.TitleId == titleId).ToDictionary(o => o.FieldName);
        return new TitleDetails(title, sources, overrides);
    }

    /// <summary>
    /// Saves values typed by the user. They win over TMDB and AniList from now on,
    /// including after a refresh. The source's value is kept so the field can be reset.
    /// </summary>
    public void SaveEdits(string titleId, IReadOnlyDictionary<string, string?> values)
    {
        foreach (var (field, value) in values)
        {
            if (TitleFieldValues.Validate(field, value) is { } error)
                throw new ArgumentException(error);
        }

        using var db = database.CreateContext();
        var title = db.Titles.Single(t => t.Id == titleId);
        var overrides = db.FieldOverrides.Where(o => o.TitleId == titleId).ToDictionary(o => o.FieldName);
        var sources = db.FieldSources.Where(s => s.TitleId == titleId).ToDictionary(s => s.FieldName);
        foreach (var (field, value) in values)
        {
            if (!overrides.TryGetValue(field, out var edit))
            {
                edit = new FieldOverride
                {
                    TitleId = titleId,
                    FieldName = field,
                    OriginalValue = TitleFieldValues.Get(title, field),
                    OriginalSource = sources.TryGetValue(field, out var src) ? src.Source : null,
                };
                db.FieldOverrides.Add(edit);
            }
            edit.Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            TitleFieldValues.Set(title, field, edit.Value);
            SetSource(db, sources, titleId, field, MetadataSource.User);
        }
        db.SaveChanges();
    }

    /// <summary>Drops the user's value for one field and brings back what the source said.</summary>
    public void ResetEdit(string titleId, string field)
    {
        using var db = database.CreateContext();
        var edit = db.FieldOverrides.SingleOrDefault(o => o.TitleId == titleId && o.FieldName == field);
        if (edit is null)
            return;
        var title = db.Titles.Single(t => t.Id == titleId);
        TitleFieldValues.Set(title, field, edit.OriginalValue);
        var source = db.FieldSources.SingleOrDefault(s => s.TitleId == titleId && s.FieldName == field);
        if (edit.OriginalSource is { } original)
        {
            if (source is null)
                db.FieldSources.Add(new FieldSource { TitleId = titleId, FieldName = field, Source = original });
            else
                source.Source = original;
        }
        else if (source is not null)
        {
            db.FieldSources.Remove(source);
        }
        db.FieldOverrides.Remove(edit);
        db.SaveChanges();
    }

    /// <summary>Every IMDb id in the library, to look up in IMDb's ratings file.</summary>
    public HashSet<string> ImdbIds(string? titleId = null)
    {
        using var db = database.CreateContext();
        return db.Titles.Where(t => t.ImdbId != null && (titleId == null || t.Id == titleId)).Select(t => t.ImdbId!).ToHashSet();
    }

    /// <summary>Stores IMDb scores by IMDb id. Returns how many titles were updated.</summary>
    public int SetImdbRatings(IReadOnlyDictionary<string, ImdbScore> scores)
    {
        using var db = database.CreateContext();
        var updated = 0;
        foreach (var title in db.Titles.Where(t => t.ImdbId != null).ToList())
        {
            if (!scores.TryGetValue(title.ImdbId!, out var score))
                continue;
            title.ImdbRating = score.Rating;
            title.ImdbVotes = score.Votes;
            updated++;
        }
        db.SaveChanges();
        return updated;
    }

    /// <summary>Sets (or clears) your rating for a whole show.</summary>
    public void SetUserRating(string titleId, double? rating)
    {
        if (!Core.Rules.Rating.IsValid(rating))
            throw new ArgumentException("Ratings run from 0.5 to 5 in half steps.");
        using var db = database.CreateContext();
        db.Titles.Where(t => t.Id == titleId).ExecuteUpdate(u => u.SetProperty(t => t.UserRating, rating));
    }

    /// <summary>For each person, how many titles they are credited on that you have watched.</summary>
    public Dictionary<string, int> WatchedTitleCounts(IEnumerable<string> personIds)
    {
        var ids = personIds.Distinct().ToList();
        using var db = database.CreateContext();
        return db.Credits
            .Where(c => ids.Contains(c.PersonId) && db.Watches.Any(w => w.TitleId == c.TitleId))
            .Select(c => new { c.PersonId, c.TitleId })
            .Distinct()
            .ToList()
            .GroupBy(c => c.PersonId)
            .ToDictionary(g => g.Key, g => g.Count());
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

    /// <summary>After Fix match, drops seasons and episodes the new match does not have, unless a watch points at them.</summary>
    private static void RemoveUnusedEpisodes(MarqueeDbContext db, Title title, List<FetchedSeason> seasons)
    {
        var keep = seasons.SelectMany(s => s.Episodes.Select(e => (s.Number, e.Number))).ToHashSet();
        var watched = db.Watches.Where(w => w.TitleId == title.Id && w.EpisodeId != null).Select(w => w.EpisodeId!).ToHashSet();
        foreach (var season in db.Seasons.Include(s => s.Episodes).Where(s => s.TitleId == title.Id).ToList())
        {
            foreach (var episode in season.Episodes.Where(e => !keep.Contains((season.Number, e.Number)) && !watched.Contains(e.Id)).ToList())
                db.Episodes.Remove(episode);
            if (season.Episodes.All(e => !keep.Contains((season.Number, e.Number)) && !watched.Contains(e.Id))
                && seasons.All(s => s.Number != season.Number))
                db.Seasons.Remove(season);
        }
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

/// <summary>A title with where each field came from and the user's edits.</summary>
public sealed record TitleDetails(
    Title Title,
    IReadOnlyDictionary<string, MetadataSource> Sources,
    IReadOnlyDictionary<string, FieldOverride> Edits);
