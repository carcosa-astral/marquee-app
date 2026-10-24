namespace Marquee.Core.Model;

/// <summary>Every record uses a string GUID so exports can be merged cleanly.</summary>
public abstract class Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
}

public class Title : Entity
{
    public TitleKind Kind { get; set; }
    public TitleFormat Format { get; set; }
    public string Name { get; set; } = "";
    public string? OriginalName { get; set; }
    public int? Year { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? Overview { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public string? Status { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public TmdbType? TmdbType { get; set; }
    public int? TmdbId { get; set; }
    public int? AniListId { get; set; }
    public int? MalId { get; set; }
    public string? ImdbId { get; set; }

    public List<Season> Seasons { get; set; } = [];
    public List<Credit> Credits { get; set; } = [];
    public List<TitleGenre> Genres { get; set; } = [];
}

public class Season : Entity
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public int Number { get; set; }
    public string? Name { get; set; }
    public List<Episode> Episodes { get; set; } = [];
}

public class Episode : Entity
{
    public string SeasonId { get; set; } = "";
    public Season? Season { get; set; }
    public int Number { get; set; }
    public string? Name { get; set; }
    public DateOnly? AirDate { get; set; }
    public int? RuntimeMinutes { get; set; }
}

public class Person : Entity
{
    public string Name { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public DateOnly? DeathDate { get; set; }
    public string? Bio { get; set; }
    public string? PhotoPath { get; set; }
    public int? TmdbId { get; set; }
    public int? AniListId { get; set; }
    public string? ImdbId { get; set; }
}

public class Credit : Entity
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public string PersonId { get; set; } = "";
    public Person? Person { get; set; }
    public CreditRole Role { get; set; }
    public string? Character { get; set; }
    public int Order { get; set; }
}

public class Genre : Entity
{
    public string Name { get; set; } = "";
}

public class TitleGenre
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public string GenreId { get; set; } = "";
    public Genre? Genre { get; set; }
}

/// <summary>One diary entry. Rewatches are separate rows.</summary>
public class Watch : Entity
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public string? EpisodeId { get; set; }
    public Episode? Episode { get; set; }
    public DateOnly WatchedOn { get; set; }
    public TimeOnly? StartTime { get; set; }
    /// <summary>0.5 to 5, in half steps.</summary>
    public double? Rating { get; set; }
    public bool IsRewatch { get; set; }
    public WatchPlace Where { get; set; }
    public string? TheaterId { get; set; }
    public Theater? Theater { get; set; }
    public string? Screen { get; set; }
    public string? Seat { get; set; }
    public string? Format { get; set; }
    public string? Service { get; set; }
    public string? Note { get; set; }
    public List<WatchCompanion> Companions { get; set; } = [];
}

/// <summary>Someone the user watches with. Created by the user.</summary>
public class Companion : Entity
{
    public string Name { get; set; } = "";
}

public class WatchCompanion
{
    public string WatchId { get; set; } = "";
    public Watch? Watch { get; set; }
    public string CompanionId { get; set; } = "";
    public Companion? Companion { get; set; }
}

/// <summary>A theater the user added. Never preloaded.</summary>
public class Theater : Entity
{
    public string Name { get; set; } = "";
    public string? City { get; set; }
    public string? Chain { get; set; }
    public List<string> Formats { get; set; } = [];
}

public class WatchlistItem : Entity
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public DateOnly AddedOn { get; set; }
    public DateOnly? ExpectedRelease { get; set; }
}

public class IceItem : Entity
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public IceState State { get; set; }
    public int? StoppedAtSeason { get; set; }
    public int? StoppedAtEpisode { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
    public DateOnly IcedOn { get; set; }
}

/// <summary>A value the user set by hand. Always wins over fetched metadata.</summary>
public class FieldOverride : Entity
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public string FieldName { get; set; } = "";
    public string? Value { get; set; }
}

/// <summary>Where a Title field's current value came from.</summary>
public class FieldSource : Entity
{
    public string TitleId { get; set; } = "";
    public Title? Title { get; set; }
    public string FieldName { get; set; } = "";
    public MetadataSource Source { get; set; }
}

public class Setting
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}
