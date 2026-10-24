namespace Marquee.Core.Metadata;

/// <summary>
/// Where a title's images were saved, as paths relative to the data folder
/// (for example "images/titles/anilist-1943-poster.jpg"), so the Marquee folder stays portable.
/// </summary>
public sealed class SavedImages
{
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }

    /// <summary>Saved person photos, keyed by the remote address they came from.</summary>
    public Dictionary<string, string> PersonPhotos { get; set; } = [];

    public static readonly SavedImages None = new();
}
