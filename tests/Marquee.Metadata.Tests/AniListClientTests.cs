using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Core.Storage;

namespace Marquee.Metadata.Tests;

public class AniListClientTests
{
    private const string Search = """
        { "data": { "Page": { "media": [
          { "id": 1943, "idMal": 1943, "format": "MOVIE", "duration": 90, "seasonYear": 2006,
            "title": { "romaji": "Paprika", "english": "Paprika", "native": "パプリカ" }, "synonyms": [],
            "coverImage": { "large": "https://al/1943.jpg" }, "genres": ["Mystery", "Sci-Fi"], "description": "A <i>dream</i>." },
          { "id": 154587, "idMal": 52991, "format": "TV", "episodes": 28, "duration": 24, "seasonYear": 2023,
            "title": { "romaji": "Sousou no Frieren", "english": "Frieren: Beyond Journey’s End", "native": "葬送のフリーレン" },
            "synonyms": ["Frieren"], "genres": ["Adventure"] }
        ] } } }
        """;

    private const string Details = """
        { "data": { "Media": {
          "id": 154587, "idMal": 52991, "format": "TV", "status": "FINISHED", "episodes": 3, "duration": 24, "seasonYear": 2023,
          "title": { "romaji": "Sousou no Frieren", "english": "Frieren: Beyond Journey’s End", "native": "葬送のフリーレン" },
          "startDate": { "year": 2023, "month": 9, "day": 29 },
          "description": "The adventure is over.<br><br>But life goes on &amp; on. (Source: Crunchyroll)",
          "coverImage": { "extraLarge": "https://al/xl.jpg", "large": "https://al/l.jpg" },
          "genres": ["Adventure", "Drama", "Fantasy"],
          "staff": { "edges": [
            { "role": "Director", "node": { "id": 1, "name": { "full": "Keiichirou Saitou" }, "image": { "large": "https://al/s.jpg" } } },
            { "role": "Episode Director (ep 1)", "node": { "id": 2, "name": { "full": "Someone Else" } } },
            { "role": "Original Creator", "node": { "id": 3, "name": { "full": "Kanehito Yamada" } } }
          ] },
          "characters": { "edges": [
            { "node": { "name": { "full": "Frieren" } },
              "voiceActors": [ { "id": 95185, "name": { "full": "Atsumi Tanezaki" }, "image": { "large": "https://al/default.jpg" },
                                 "dateOfBirth": { "year": 1989, "month": 9, "day": 27 } } ] },
            { "node": { "name": { "full": "Fern" } }, "voiceActors": [] }
          ] } } } }
        """;

    [Fact]
    public async Task Search_reads_names_formats_and_mal_ids()
    {
        var http = new FakeHttp().OnBody("Page", Search);

        var hits = await new AniListClient(http.Client()).SearchAsync("paprika");

        Assert.Equal(2, hits.Count);
        Assert.Equal((TitleFormat.Film, 1943, 2006), (hits[0].Format, hits[0].MalId!.Value, hits[0].Year!.Value));
        Assert.Equal("A dream.", hits[0].Overview);
        Assert.Equal((TitleFormat.Series, 28), (hits[1].Format, hits[1].Episodes!.Value));
        Assert.Contains("Sousou no Frieren", hits[1].Names);
        Assert.Contains("Frieren", hits[1].Names);
        Assert.Contains("\"search\":\"paprika\"", http.Bodies[0]);
    }

    [Fact]
    public async Task Details_map_directors_voices_episodes_and_clean_text()
    {
        var http = new FakeHttp().OnBody("Media(id", Details);

        var t = await new AniListClient(http.Client()).GetAsync(154587);

        Assert.Equal("Frieren: Beyond Journey’s End", t.Name);
        Assert.Equal("葬送のフリーレン", t.OriginalName);
        Assert.Equal(new DateOnly(2023, 9, 29), t.ReleaseDate);
        Assert.Equal("Finished", t.Status);
        Assert.Equal("https://al/xl.jpg", t.PosterUrl);
        Assert.Null(t.BackdropUrl);
        Assert.Equal("The adventure is over.\n\nBut life goes on & on. (Source: Crunchyroll)", t.Overview);
        Assert.Equal(MetadataSource.AniList, t.Sources[TitleFields.Name]);

        Assert.Equal("Keiichirou Saitou", Assert.Single(t.Credits, c => c.Role == CreditRole.Director).Person.Name);
        var voice = Assert.Single(t.Credits, c => c.Role == CreditRole.Voice);
        Assert.Equal(("Atsumi Tanezaki", "Frieren", 95185), (voice.Person.Name, voice.Character, voice.Person.AniListId!.Value));
        Assert.Null(voice.Person.PhotoUrl);
        Assert.Equal(new DateOnly(1989, 9, 27), voice.Person.BirthDate);

        var season = Assert.Single(t.Seasons);
        Assert.Equal([1, 2, 3], season.Episodes.Select(e => e.Number));
    }

    [Theory]
    [InlineData("Director", true)]
    [InlineData("Chief Director", true)]
    [InlineData("Director (eps 1-12)", true)]
    [InlineData("Episode Director (ep 3)", false)]
    [InlineData("Animation Director", false)]
    [InlineData("Sound Director", false)]
    public void Only_series_and_chief_directors_count(string role, bool counted) =>
        Assert.Equal(counted, AniListClient.IsDirectorRole(role));

    [Fact]
    public async Task Search_still_works_when_tmdb_has_no_key()
    {
        var http = new FakeHttp().OnBody("Page", Search);
        var service = new MetadataService(new TmdbClient(http.Client(), () => null), new AniListClient(http.Client()));

        var outcome = await service.SearchAsync("paprika");

        Assert.Equal(2, outcome.Results.Count);
        Assert.NotNull(outcome.TmdbProblem);
        Assert.Null(outcome.AniListProblem);
        Assert.All(http.Requests, r => Assert.Equal("graphql.anilist.co", r.RequestUri!.Host));
    }

    [Fact]
    public async Task Images_are_saved_inside_the_data_folder_only()
    {
        var root = Path.Combine(Path.GetTempPath(), "marquee-tests", Guid.NewGuid().ToString("N"));
        var folder = MarqueeFolder.AtRoot(root);
        try
        {
            var http = new FakeHttp().OnBytes("al/xl.jpg", [1, 2, 3]);
            var downloader = new ImageDownloader(http.Client(), folder);

            var path = await downloader.SaveAsync("https://al/xl.jpg", "titles", "anilist-1-poster");
            var insecure = await downloader.SaveAsync("http://al/xl.jpg", "titles", "x");
            var escape = await downloader.SaveAsync("https://al/xl.jpg", "../../..", "x");

            Assert.Equal("images/titles/anilist-1-poster.jpg", path);
            Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(folder.Data, path!)));
            Assert.Null(insecure);
            Assert.Null(escape);
            Assert.All(Directory.GetFiles(root, "*", SearchOption.AllDirectories), f => Assert.StartsWith(folder.Images, f));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
