namespace Marquee.Core.Model;

public enum TitleKind { Movie, Tv, Anime }

public enum TitleFormat { Movie, Series, Ova, Ona, Special, Film }

public enum CreditRole { Director, Actor, Voice }

public enum WatchPlace { Theater, Home, Streaming, Other }

public enum IceState { Paused, Dropped }

public enum MetadataSource { Tmdb, AniList, User }

/// <summary>TMDB keeps movie and TV ids apart, so a TMDB id is only unique together with its type.</summary>
public enum TmdbType { Movie, Tv }
