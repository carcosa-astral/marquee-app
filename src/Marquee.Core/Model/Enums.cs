namespace Marquee.Core.Model;

public enum TitleKind { Movie, Tv, Anime }

public enum TitleFormat { Movie, Series, Ova, Ona, Special, Film }

public enum CreditRole { Director, Actor, Voice }

/// <summary>Where a watch happened. Streaming services are recorded as the service on a Home or Other watch.</summary>
public enum WatchPlace { Theater, Home, Other }

public enum IceState { Paused, Dropped }

public enum MetadataSource { Tmdb, AniList, User }

/// <summary>TMDB keeps movie and TV ids apart, so a TMDB id is only unique together with its type.</summary>
public enum TmdbType { Movie, Tv }
