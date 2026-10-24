namespace Marquee.Metadata;

/// <summary>A problem talking to TMDB or AniList, with a message that can be shown to the user.</summary>
public sealed class MetadataException(string message, Exception? inner = null) : Exception(message, inner);
