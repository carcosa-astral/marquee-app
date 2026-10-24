using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Marquee.Core.Metadata;
using Marquee.Core.Model;

namespace Marquee.Metadata;

/// <summary>
/// Reads movie and TV metadata from TMDB (API v3). Accepts either the short API key
/// or the longer read access token from the user's TMDB account.
/// </summary>
public sealed class TmdbClient(HttpClient http, Func<string?> apiKey)
{
    public const string BaseUrl = "https://api.themoviedb.org/3/";
    public const string ImageBaseUrl = "https://image.tmdb.org/t/p/";

    private const int AnimationGenre = 16;
    private const int MaxCast = 20;
    private const int MaxTvDirectors = 5;

    public bool HasKey => !string.IsNullOrWhiteSpace(apiKey());

    public async Task<List<SearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        using var doc = await GetAsync($"search/multi?query={Uri.EscapeDataString(query)}&include_adult=false&language=en-US&page=1", ct);
        var hits = new List<SearchHit>();
        foreach (var r in doc.RootElement.Items("results"))
        {
            var type = r.Str("media_type") switch { "movie" => TmdbType.Movie, "tv" => TmdbType.Tv, _ => (TmdbType?)null };
            if (type is null || r.Int("id") is not { } id)
                continue;
            var genreIds = r.Items("genre_ids").Select(g => g.GetInt32()).ToList();
            var anime = IsAnime(genreIds, r.Items("origin_country").Select(c => c.GetString()), r.Str("original_language"));
            var name = type == TmdbType.Movie ? r.Str("title") : r.Str("name");
            var original = type == TmdbType.Movie ? r.Str("original_title") : r.Str("original_name");
            var genreNames = type == TmdbType.Movie ? MovieGenres : TvGenres;
            hits.Add(new SearchHit
            {
                Source = MetadataSource.Tmdb,
                Id = id,
                TmdbType = type,
                Kind = anime ? TitleKind.Anime : type == TmdbType.Movie ? TitleKind.Movie : TitleKind.Tv,
                Format = FormatFor(type.Value, anime),
                Name = name ?? original ?? "",
                Names = new[] { name, original }.OfType<string>().ToList(),
                Year = Json.Year(type == TmdbType.Movie ? r.Str("release_date") : r.Str("first_air_date")),
                Overview = r.Str("overview"),
                PosterUrl = Image("w185", r.Str("poster_path")),
                Genres = genreIds.Select(g => genreNames.GetValueOrDefault(g)).OfType<string>().ToList(),
                Rank = hits.Count,
            });
        }
        return hits;
    }

    public Task<FetchedTitle> GetAsync(TmdbType type, int id, CancellationToken ct = default) =>
        type == TmdbType.Movie ? GetMovieAsync(id, ct) : GetTvAsync(id, ct);

    public async Task<FetchedTitle> GetMovieAsync(int id, CancellationToken ct = default)
    {
        using var doc = await GetAsync($"movie/{id}?append_to_response=credits&language=en-US", ct);
        var m = doc.RootElement;
        var genres = m.Items("genres").ToList();
        var anime = IsAnime(genres.Select(g => g.Int("id") ?? 0), m.Items("origin_country").Select(c => c.GetString()), m.Str("original_language"));
        var castRole = anime ? CreditRole.Voice : CreditRole.Actor;

        var credits = m.Prop("credits")
            .Items("crew")
            .Where(c => c.Str("job") == "Director")
            .Select((c, i) => Credit(c, CreditRole.Director, null, i))
            .ToList();
        credits.AddRange(m.Prop("credits")
            .Items("cast")
            .Where(c => CreditRules.IsCountedCastRole(c.Str("character")))
            .OrderBy(c => c.Int("order") ?? int.MaxValue)
            .Take(MaxCast)
            .Select((c, i) => Credit(c, castRole, c.Str("character"), i)));

        return new FetchedTitle
        {
            Kind = anime ? TitleKind.Anime : TitleKind.Movie,
            Format = FormatFor(TmdbType.Movie, anime),
            TmdbType = TmdbType.Movie,
            TmdbId = id,
            ImdbId = m.Str("imdb_id"),
            Name = m.Str("title"),
            OriginalName = m.Str("original_title"),
            ReleaseDate = Json.Date(m.Str("release_date")),
            Year = Json.Year(m.Str("release_date")),
            Runtime = m.Int("runtime"),
            Overview = m.Str("overview"),
            Status = m.Str("status"),
            PosterUrl = Image("w500", m.Str("poster_path")),
            BackdropUrl = Image("w1280", m.Str("backdrop_path")),
            Genres = genres.Select(g => g.Str("name")).OfType<string>().ToList(),
            Credits = credits,
        }.WithSource(MetadataSource.Tmdb);
    }

    public async Task<FetchedTitle> GetTvAsync(int id, CancellationToken ct = default)
    {
        using var doc = await GetAsync($"tv/{id}?append_to_response=aggregate_credits,external_ids&language=en-US", ct);
        var t = doc.RootElement;
        var genres = t.Items("genres").ToList();
        var genreIds = genres.Select(g => g.Int("id") ?? 0).ToList();
        var anime = IsAnime(genreIds, t.Items("origin_country").Select(c => c.GetString()), t.Str("original_language"));
        var castRole = anime ? CreditRole.Voice : CreditRole.Actor;

        var aggregate = t.Prop("aggregate_credits");
        var credits = aggregate
            .Items("crew")
            .Where(c => c.Items("jobs").Any(j => j.Str("job") == "Director"))
            .OrderByDescending(c => c.Int("total_episode_count") ?? 0)
            .Take(MaxTvDirectors)
            .Select((c, i) => Credit(c, CreditRole.Director, null, i))
            .ToList();

        // Talk shows and news never count toward person pages, so their cast is skipped.
        if (!genreIds.Any(g => g is TalkGenre or NewsGenre))
        {
            credits.AddRange(aggregate
                .Items("cast")
                .Select(c => (Cast: c, Character: c.Items("roles").Select(r => r.Str("character")).FirstOrDefault()))
                .Where(c => CreditRules.IsCountedCastRole(c.Character))
                .OrderBy(c => c.Cast.Int("order") ?? int.MaxValue)
                .Take(MaxCast)
                .Select((c, i) => Credit(c.Cast, castRole, c.Character, i)));
        }

        var runtime = t.Items("episode_run_time").Select(r => (int?)r.GetInt32()).FirstOrDefault()
            ?? t.Prop("last_episode_to_air")?.Int("runtime");

        var seasonNumbers = t.Items("seasons").Select(s => s.Int("season_number")).OfType<int>().ToList();
        var seasons = new List<FetchedSeason>();
        foreach (var number in seasonNumbers)
            seasons.Add(await GetSeasonAsync(id, number, ct));

        return new FetchedTitle
        {
            Kind = anime ? TitleKind.Anime : TitleKind.Tv,
            Format = TitleFormat.Series,
            TmdbType = TmdbType.Tv,
            TmdbId = id,
            ImdbId = t.Prop("external_ids")?.Str("imdb_id"),
            Name = t.Str("name"),
            OriginalName = t.Str("original_name"),
            ReleaseDate = Json.Date(t.Str("first_air_date")),
            Year = Json.Year(t.Str("first_air_date")),
            Runtime = runtime,
            Overview = t.Str("overview"),
            Status = t.Str("status"),
            PosterUrl = Image("w500", t.Str("poster_path")),
            BackdropUrl = Image("w1280", t.Str("backdrop_path")),
            Genres = genres.Select(g => g.Str("name")).OfType<string>().ToList(),
            Credits = credits,
            Seasons = seasons,
        }.WithSource(MetadataSource.Tmdb);
    }

    private async Task<FetchedSeason> GetSeasonAsync(int tvId, int number, CancellationToken ct)
    {
        using var doc = await GetAsync($"tv/{tvId}/season/{number}?language=en-US", ct);
        var s = doc.RootElement;
        return new FetchedSeason
        {
            Number = number,
            Name = s.Str("name"),
            Episodes = s.Items("episodes")
                .Where(e => e.Int("episode_number") is not null)
                .Select(e => new FetchedEpisode
                {
                    Number = e.Int("episode_number")!.Value,
                    Name = e.Str("name"),
                    AirDate = Json.Date(e.Str("air_date")),
                    Runtime = e.Int("runtime"),
                })
                .ToList(),
        };
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        var key = apiKey()?.Trim();
        if (string.IsNullOrEmpty(key))
            throw new MetadataException("Add your TMDB key in Settings to search movies and TV.");

        // A read access token is a long JWT; the classic API key goes in the query string.
        var isToken = key.StartsWith("eyJ", StringComparison.Ordinal);
        var url = BaseUrl + path + (isToken ? "" : (path.Contains('?') ? "&" : "?") + "api_key=" + Uri.EscapeDataString(key));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (isToken)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new MetadataException("Could not reach TMDB. Check your internet connection.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new MetadataException("TMDB did not accept your key. Check it in Settings.");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new MetadataException("TMDB is busy. Try again in a moment.");
            if (!response.IsSuccessStatusCode)
                throw new MetadataException($"TMDB returned an error ({(int)response.StatusCode}).");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }

    private static FetchedCredit Credit(JsonElement c, CreditRole role, string? character, int order) => new()
    {
        Role = role,
        Character = character,
        Order = order,
        Person = new FetchedPerson
        {
            Name = c.Str("name") ?? "",
            TmdbId = c.Int("id"),
            PhotoUrl = Image("w185", c.Str("profile_path")),
        },
    };

    /// <summary>Japanese animation is treated as anime.</summary>
    private static bool IsAnime(IEnumerable<int> genreIds, IEnumerable<string?> countries, string? language) =>
        genreIds.Contains(AnimationGenre) && (countries.Contains("JP") || language == "ja");

    private static TitleFormat FormatFor(TmdbType type, bool anime) =>
        type == TmdbType.Tv ? TitleFormat.Series : anime ? TitleFormat.Film : TitleFormat.Movie;

    private static string? Image(string size, string? path) => path is null ? null : ImageBaseUrl + size + path;

    private const int TalkGenre = 10767;
    private const int NewsGenre = 10763;

    // TMDB's genre lists. Search results only carry genre ids.
    private static readonly Dictionary<int, string> MovieGenres = new()
    {
        [28] = "Action", [12] = "Adventure", [16] = "Animation", [35] = "Comedy", [80] = "Crime",
        [99] = "Documentary", [18] = "Drama", [10751] = "Family", [14] = "Fantasy", [36] = "History",
        [27] = "Horror", [10402] = "Music", [9648] = "Mystery", [10749] = "Romance", [878] = "Science Fiction",
        [10770] = "TV Movie", [53] = "Thriller", [10752] = "War", [37] = "Western",
    };

    private static readonly Dictionary<int, string> TvGenres = new()
    {
        [10759] = "Action & Adventure", [16] = "Animation", [35] = "Comedy", [80] = "Crime",
        [99] = "Documentary", [18] = "Drama", [10751] = "Family", [10762] = "Kids", [9648] = "Mystery",
        [10763] = "News", [10764] = "Reality", [10765] = "Sci-Fi & Fantasy", [10766] = "Soap",
        [10767] = "Talk", [10768] = "War & Politics", [37] = "Western",
    };
}
