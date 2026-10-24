using System.Net;
using Marquee.Core.Model;

namespace Marquee.Metadata.Tests;

public class TmdbClientTests
{
    private const string Search = """
        { "results": [
          { "media_type": "movie", "id": 4977, "title": "Paprika", "original_title": "パプリカ", "release_date": "2006-11-25",
            "genre_ids": [16, 878], "original_language": "ja", "poster_path": "/p.jpg", "overview": "Dream detective." },
          { "media_type": "movie", "id": 11, "title": "Heat", "original_title": "Heat", "release_date": "1995-12-15",
            "genre_ids": [80, 18], "original_language": "en" },
          { "media_type": "tv", "id": 209867, "name": "Frieren: Beyond Journey's End", "original_name": "葬送のフリーレン",
            "first_air_date": "2023-09-29", "genre_ids": [16, 10759], "origin_country": ["JP"] },
          { "media_type": "person", "id": 1, "name": "Satoshi Kon" }
        ] }
        """;

    private const string Movie = """
        { "id": 949, "title": "Heat", "original_title": "Heat", "release_date": "1995-12-15", "runtime": 170,
          "overview": "Obsessive master thief.", "status": "Released", "imdb_id": "tt0113277",
          "poster_path": "/heat.jpg", "backdrop_path": "/heat-bg.jpg", "original_language": "en",
          "genres": [ { "id": 80, "name": "Crime" }, { "id": 18, "name": "Drama" } ],
          "credits": {
            "cast": [
              { "id": 1158, "name": "Al Pacino", "character": "Lt. Vincent Hanna", "order": 0, "profile_path": "/al.jpg" },
              { "id": 380, "name": "Robert De Niro", "character": "Neil McCauley", "order": 1 },
              { "id": 999, "name": "Someone", "character": "Self (archive footage)", "order": 2 }
            ],
            "crew": [ { "id": 638, "name": "Michael Mann", "job": "Director" }, { "id": 5, "name": "Writer", "job": "Screenplay" } ]
          } }
        """;

    private static TmdbClient Client(FakeHttp http, string? key = "abc123") => new(http.Client(), () => key);

    [Fact]
    public async Task Search_keeps_movies_and_tv_and_spots_anime()
    {
        var http = new FakeHttp().On("search/multi", Search);

        var hits = await Client(http).SearchAsync("paprika");

        Assert.Equal(3, hits.Count);
        var paprika = hits[0];
        Assert.Equal((TitleKind.Anime, TitleFormat.Film, TmdbType.Movie, 2006), (paprika.Kind, paprika.Format, paprika.TmdbType!.Value, paprika.Year!.Value));
        Assert.Contains("パプリカ", paprika.Names);
        Assert.Equal(["Animation", "Science Fiction"], paprika.Genres);
        Assert.Equal("https://image.tmdb.org/t/p/w185/p.jpg", paprika.PosterUrl);
        Assert.Equal(TitleKind.Movie, hits[1].Kind);
        Assert.Equal((TitleKind.Anime, TitleFormat.Series), (hits[2].Kind, hits[2].Format));
        Assert.Equal([0, 1, 2], hits.Select(h => h.Rank));
    }

    [Fact]
    public async Task Api_key_goes_in_the_query_and_a_token_in_the_header()
    {
        var http = new FakeHttp().On("search/multi", Search);
        await Client(http, "abc123").SearchAsync("x");
        await Client(http, "eyJhbGciOi.token").SearchAsync("x");

        Assert.Contains("api_key=abc123", http.Requests[0].RequestUri!.Query);
        Assert.Null(http.Requests[0].Headers.Authorization);
        Assert.DoesNotContain("api_key", http.Requests[1].RequestUri!.Query);
        Assert.Equal("Bearer", http.Requests[1].Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task A_rejected_key_gives_a_clear_message()
    {
        var http = new FakeHttp().On("search/multi", "{}", HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<MetadataException>(() => Client(http).SearchAsync("x"));
        Assert.Contains("did not accept your key", ex.Message);
    }

    [Fact]
    public async Task No_key_means_no_request()
    {
        var http = new FakeHttp();

        await Assert.ThrowsAsync<MetadataException>(() => Client(http, null).SearchAsync("x"));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Movie_details_include_director_and_counted_cast()
    {
        var http = new FakeHttp().On("movie/949", Movie);

        var heat = await Client(http).GetMovieAsync(949);

        Assert.Equal(("Heat", 1995, 170, "tt0113277"), (heat.Name, heat.Year!.Value, heat.Runtime!.Value, heat.ImdbId));
        Assert.Equal(new DateOnly(1995, 12, 15), heat.ReleaseDate);
        Assert.Equal("https://image.tmdb.org/t/p/w500/heat.jpg", heat.PosterUrl);
        Assert.Equal("https://image.tmdb.org/t/p/w1280/heat-bg.jpg", heat.BackdropUrl);
        Assert.Equal(["Crime", "Drama"], heat.Genres);
        Assert.Equal(MetadataSource.Tmdb, heat.Sources["overview"]);

        Assert.Equal("Michael Mann", Assert.Single(heat.Credits, c => c.Role == CreditRole.Director).Person.Name);
        var cast = heat.Credits.Where(c => c.Role == CreditRole.Actor).ToList();
        Assert.Equal(["Al Pacino", "Robert De Niro"], cast.Select(c => c.Person.Name));
        Assert.Equal("https://image.tmdb.org/t/p/w185/al.jpg", cast[0].Person.PhotoUrl);
    }

    [Fact]
    public async Task Tv_details_include_seasons_and_skip_talk_show_cast()
    {
        var http = new FakeHttp()
            .On("tv/1/season/1", """{ "name": "Season 1", "episodes": [ { "episode_number": 1, "name": "Pilot", "air_date": "2020-01-01", "runtime": 30 }, { "episode_number": 2 } ] }""")
            .On("tv/1?", """
                { "id": 1, "name": "Late Show", "first_air_date": "2020-01-01", "episode_run_time": [30],
                  "genres": [ { "id": 10767, "name": "Talk" } ], "origin_country": ["US"],
                  "external_ids": { "imdb_id": "tt1" }, "seasons": [ { "season_number": 1 } ],
                  "aggregate_credits": {
                    "cast": [ { "id": 7, "name": "Host", "roles": [ { "character": "Host" } ], "order": 0 } ],
                    "crew": [ { "id": 8, "name": "Dir", "jobs": [ { "job": "Director" } ], "total_episode_count": 10 } ] } }
                """);

        var show = await Client(http).GetTvAsync(1);

        Assert.Equal((TitleKind.Tv, TitleFormat.Series, 30, "tt1"), (show.Kind, show.Format, show.Runtime!.Value, show.ImdbId));
        var season = Assert.Single(show.Seasons);
        Assert.Equal(2, season.Episodes.Count);
        Assert.Equal("Pilot", season.Episodes[0].Name);
        Assert.DoesNotContain(show.Credits, c => c.Role == CreditRole.Actor);
        Assert.Single(show.Credits, c => c.Role == CreditRole.Director);
    }
}
