using System.IO.Compression;
using System.Text;
using Marquee.Core.Storage;

namespace Marquee.Metadata.Tests;

public class ImdbRatingsFileTests
{
    private static MemoryStream Gzip(string text)
    {
        var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            gzip.Write(bytes);
        }
        output.Position = 0;
        return output;
    }

    private const string Sample = "tconst\taverageRating\tnumVotes\ntt0000001\t5.7\t2100\ntt0113277\t8.3\t712345\ntt0851578\t7.7\t95000\nbroken line\n";

    [Fact]
    public void Reads_only_the_ids_asked_for()
    {
        var scores = ImdbRatingsFile.Read(Gzip(Sample), new HashSet<string> { "tt0113277", "tt0851578", "tt9999999" });

        Assert.Equal(2, scores.Count);
        Assert.Equal(new(8.3, 712345), scores["tt0113277"]);
        Assert.Equal(new(7.7, 95000), scores["tt0851578"]);
    }

    [Fact]
    public async Task Downloads_into_the_data_folder_and_reads_back()
    {
        var root = Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N"));
        var folder = MarqueeFolder.AtRoot(root);
        try
        {
            var http = new FakeHttp().OnBytes("datasets.imdbws.com", Gzip(Sample).ToArray());
            var file = new ImdbRatingsFile(http.Client(), folder);
            Assert.False(file.Exists);
            Assert.Empty(await file.ReadAsync(new HashSet<string> { "tt0113277" }));

            await file.DownloadAsync();

            Assert.True(file.Exists);
            Assert.StartsWith(folder.Data, file.FilePath);
            Assert.NotNull(file.DownloadedAt);
            Assert.Equal(8.3, (await file.ReadAsync(new HashSet<string> { "tt0113277" }))["tt0113277"].Rating);
            Assert.All(Directory.GetFiles(root, "*", SearchOption.AllDirectories), f => Assert.True(folder.Contains(f)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_failed_download_keeps_the_old_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N"));
        var folder = MarqueeFolder.AtRoot(root);
        try
        {
            var good = new ImdbRatingsFile(new FakeHttp().OnBytes("datasets.imdbws.com", Gzip(Sample).ToArray()).Client(), folder);
            await good.DownloadAsync();

            var bad = new ImdbRatingsFile(new FakeHttp().On("datasets.imdbws.com", "{}", System.Net.HttpStatusCode.ServiceUnavailable).Client(), folder);
            await Assert.ThrowsAsync<MetadataException>(() => bad.DownloadAsync());

            Assert.Equal(8.3, (await good.ReadAsync(new HashSet<string> { "tt0113277" }))["tt0113277"].Rating);
            Assert.False(File.Exists(good.FilePath + ".part"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
