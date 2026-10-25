using System.Globalization;
using System.IO.Compression;
using Marquee.Core.Metadata;
using Marquee.Core.Storage;

namespace Marquee.Metadata;

/// <summary>
/// IMDb's public ratings file (title.ratings.tsv.gz), saved in data/imdb/ inside the Marquee folder.
/// It lists the average rating and vote count for every title on IMDb, by IMDb id.
/// Information courtesy of IMDb (https://www.imdb.com). Used with permission, for personal use.
/// </summary>
public sealed class ImdbRatingsFile(HttpClient http, MarqueeFolder folder)
{
    public const string Url = "https://datasets.imdbws.com/title.ratings.tsv.gz";

    public string FilePath { get; } = Path.Combine(folder.Data, "imdb", "title.ratings.tsv.gz");

    public bool Exists => File.Exists(FilePath);

    /// <summary>When the saved file was downloaded, or null when there is none.</summary>
    public DateTime? DownloadedAt => Exists ? File.GetLastWriteTime(FilePath) : null;

    /// <summary>Downloads the latest file. The old one is kept until the new one is complete.</summary>
    public async Task DownloadAsync(CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        if (!folder.Contains(dir))
            throw new InvalidOperationException("The IMDb file must stay inside the Marquee folder.");
        Directory.CreateDirectory(dir);
        var part = FilePath + ".part";
        try
        {
            using var response = await http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new MetadataException($"IMDb returned an error ({(int)response.StatusCode}).");
            await using (var file = File.Create(part))
                await response.Content.CopyToAsync(file, ct);
            File.Move(part, FilePath, overwrite: true);
        }
        catch (HttpRequestException ex)
        {
            throw new MetadataException("Could not reach IMDb. Check your internet connection.", ex);
        }
        finally
        {
            if (File.Exists(part))
                File.Delete(part);
        }
    }

    /// <summary>Reads the scores for the given IMDb ids from the saved file. Empty when there is no file.</summary>
    public Task<Dictionary<string, ImdbScore>> ReadAsync(IReadOnlySet<string> imdbIds, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            if (!Exists || imdbIds.Count == 0)
                return new Dictionary<string, ImdbScore>();
            using var stream = File.OpenRead(FilePath);
            return Read(stream, imdbIds, ct);
        }, ct);

    /// <summary>Reads a gzipped ratings file: a header line, then "tconst  averageRating  numVotes" per line.</summary>
    internal static Dictionary<string, ImdbScore> Read(Stream gzipped, IReadOnlySet<string> imdbIds, CancellationToken ct = default)
    {
        var scores = new Dictionary<string, ImdbScore>();
        using var gzip = new GZipStream(gzipped, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0 || !imdbIds.Contains(line[..tab]))
                continue;
            ct.ThrowIfCancellationRequested();
            var parts = line.Split('\t');
            if (parts.Length >= 3
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var rating)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var votes))
            {
                scores[parts[0]] = new ImdbScore(rating, votes);
            }
            if (scores.Count == imdbIds.Count)
                break;
        }
        return scores;
    }
}
