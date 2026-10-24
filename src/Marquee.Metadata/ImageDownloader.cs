using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Core.Storage;

namespace Marquee.Metadata;

/// <summary>Saves posters, backdrops and person photos into data/images. Never writes anywhere else.</summary>
public sealed class ImageDownloader(HttpClient http, MarqueeFolder folder)
{
    /// <summary>Directors plus the first cast members get their photos saved.</summary>
    public const int MaxCastPhotos = 12;

    public async Task<SavedImages> SaveAllAsync(FetchedTitle title, CancellationToken ct = default)
    {
        var key = TitleKey(title);
        var saved = new SavedImages
        {
            PosterPath = await SaveAsync(title.PosterUrl, "titles", key + "-poster", ct),
            BackdropPath = await SaveAsync(title.BackdropUrl, "titles", key + "-backdrop", ct),
        };

        var people = title.Credits.Where(c => c.Role == CreditRole.Director)
            .Concat(title.Credits.Where(c => c.Role != CreditRole.Director).Take(MaxCastPhotos))
            .Select(c => c.Person)
            .Where(p => p.PhotoUrl is not null && PersonKey(p) is not null)
            .DistinctBy(p => p.PhotoUrl)
            .ToList();

        using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(people.Select(async p =>
        {
            await gate.WaitAsync(ct);
            try
            {
                if (await SaveAsync(p.PhotoUrl, "people", PersonKey(p)!, ct) is { } path)
                {
                    lock (saved.PersonPhotos)
                        saved.PersonPhotos[p.PhotoUrl!] = path;
                }
            }
            finally
            {
                gate.Release();
            }
        }));

        return saved;
    }

    /// <summary>
    /// Downloads one image to data/images/{group}/{name}.ext and returns its path relative
    /// to the data folder, or null if there is no image or the download failed.
    /// An image that is already saved is kept as it is.
    /// </summary>
    public async Task<string?> SaveAsync(string? url, string group, string name, CancellationToken ct = default)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;

        var relative = $"images/{group}/{name}{Extension(uri)}";
        var full = Path.GetFullPath(Path.Combine(folder.Data, relative));
        if (!folder.Contains(full))
            return null;
        if (File.Exists(full))
            return relative;

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var part = full + ".part";
        try
        {
            using var response = await http.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
                return null;
            await using (var file = File.Create(part))
                await response.Content.CopyToAsync(file, ct);
            File.Move(part, full, overwrite: true);
            return relative;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            File.Delete(part);
            return null;
        }
    }

    public static string TitleKey(FetchedTitle t) =>
        t.AniListId is { } al ? $"anilist-{al}"
        : t.TmdbId is { } id ? $"tmdb-{(t.TmdbType == TmdbType.Tv ? "tv" : "movie")}-{id}"
        : Guid.NewGuid().ToString("N");

    private static string? PersonKey(FetchedPerson p) =>
        p.TmdbId is { } t ? $"tmdb-{t}" : p.AniListId is { } a ? $"anilist-{a}" : null;

    private static string Extension(Uri uri)
    {
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" ? ext : ".jpg";
    }
}
