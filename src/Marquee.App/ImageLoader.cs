using Avalonia.Media.Imaging;
using Marquee.Core.Storage;

namespace Marquee.App;

/// <summary>
/// Loads posters for display. Saved images come from data/images; search thumbnails are
/// kept in memory only, so browsing results never writes files.
/// </summary>
public sealed class ImageLoader(HttpClient http, MarqueeFolder folder)
{
    private readonly Dictionary<string, Bitmap> _cache = [];

    public async Task<Bitmap?> LoadRemoteAsync(string? url, int width)
    {
        if (url is null)
            return null;
        if (_cache.TryGetValue(url, out var cached))
            return cached;
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            var bitmap = await Task.Run(() => Bitmap.DecodeToWidth(new MemoryStream(bytes), width));
            return _cache[url] = bitmap;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Loads an image saved in the data folder, from its path relative to that folder.</summary>
    public async Task<Bitmap?> LoadSavedAsync(string? relativePath, int width)
    {
        if (relativePath is null)
            return null;
        var full = Path.GetFullPath(Path.Combine(folder.Data, relativePath));
        if (!folder.Contains(full) || !File.Exists(full))
            return null;
        var key = "file:" + full;
        if (_cache.TryGetValue(key, out var cached))
            return cached;
        try
        {
            var bitmap = await Task.Run(() =>
            {
                using var stream = File.OpenRead(full);
                return Bitmap.DecodeToWidth(stream, width);
            });
            return _cache[key] = bitmap;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
