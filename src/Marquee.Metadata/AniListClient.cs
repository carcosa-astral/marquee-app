using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Marquee.Core.Metadata;
using Marquee.Core.Model;

namespace Marquee.Metadata;

/// <summary>Reads anime metadata from AniList's public GraphQL API. No key needed.</summary>
public sealed partial class AniListClient(HttpClient http)
{
    public const string Endpoint = "https://graphql.anilist.co";

    private const string SearchQuery = """
        query ($search: String) {
          Page(perPage: 10) {
            media(search: $search, type: ANIME, isAdult: false, sort: SEARCH_MATCH) {
              id idMal format episodes duration seasonYear
              title { romaji english native }
              synonyms
              startDate { year }
              coverImage { large }
              genres
              description(asHtml: false)
            }
          }
        }
        """;

    private const string DetailsQuery = """
        query ($id: Int) {
          Media(id: $id, type: ANIME) {
            id idMal format status episodes duration seasonYear
            title { romaji english native }
            startDate { year month day }
            description(asHtml: false)
            coverImage { extraLarge large }
            genres
            staff(sort: [RELEVANCE, ID], perPage: 25) {
              edges { role node { id name { full } image { large } } }
            }
            characters(sort: [ROLE, RELEVANCE, ID], perPage: 25) {
              edges {
                node { name { full } }
                voiceActors(language: JAPANESE, sort: [RELEVANCE, ID]) {
                  id name { full } image { large } dateOfBirth { year month day }
                }
              }
            }
          }
        }
        """;

    public async Task<List<SearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        using var doc = await PostAsync(SearchQuery, new { search = query }, ct);
        var media = doc.RootElement.Prop("data")?.Prop("Page")?.Items("media") ?? [];
        var hits = new List<SearchHit>();
        foreach (var m in media)
        {
            if (m.Int("id") is not { } id)
                continue;
            var title = m.Prop("title");
            var english = title?.Str("english");
            var romaji = title?.Str("romaji");
            hits.Add(new SearchHit
            {
                Source = MetadataSource.AniList,
                Id = id,
                MalId = m.Int("idMal"),
                Kind = TitleKind.Anime,
                Format = MapFormat(m.Str("format")),
                Name = english ?? romaji ?? "",
                Names = new[] { english, romaji, title?.Str("native") }
                    .Concat(m.Items("synonyms").Select(s => s.GetString()))
                    .OfType<string>()
                    .ToList(),
                Year = m.Int("seasonYear") ?? m.Prop("startDate")?.Int("year"),
                Overview = CleanDescription(m.Str("description")),
                PosterUrl = m.Prop("coverImage")?.Str("large"),
                Genres = m.Items("genres").Select(g => g.GetString()).OfType<string>().ToList(),
                Episodes = m.Int("episodes"),
                Runtime = m.Int("duration"),
                Rank = hits.Count,
            });
        }
        return hits;
    }

    public async Task<FetchedTitle> GetAsync(int id, CancellationToken ct = default)
    {
        using var doc = await PostAsync(DetailsQuery, new { id }, ct);
        var m = doc.RootElement.Prop("data")?.Prop("Media")
            ?? throw new MetadataException("AniList has no anime with that id.");

        var title = m.Prop("title");
        var format = MapFormat(m.Str("format"));
        var start = m.Prop("startDate");

        var credits = m.Prop("staff")
            .Items("edges")
            .Where(e => IsDirectorRole(e.Str("role")))
            .Select(e => e.Prop("node"))
            .OfType<JsonElement>()
            .DistinctBy(n => n.Int("id"))
            .Select((n, i) => new FetchedCredit { Role = CreditRole.Director, Order = i, Person = Person(n, null) })
            .ToList();

        var order = 0;
        foreach (var edge in m.Prop("characters").Items("edges"))
        {
            var character = edge.Prop("node")?.Prop("name")?.Str("full");
            foreach (var va in edge.Items("voiceActors").Take(1))
            {
                credits.Add(new FetchedCredit
                {
                    Role = CreditRole.Voice,
                    Character = character,
                    Order = order++,
                    Person = Person(va, va.Prop("dateOfBirth")),
                });
            }
        }

        var episodes = m.Int("episodes");
        var duration = m.Int("duration");
        var seasons = format == TitleFormat.Film || episodes is not > 0
            ? []
            : new List<FetchedSeason>
            {
                new()
                {
                    Number = 1,
                    Episodes = Enumerable.Range(1, episodes.Value)
                        .Select(n => new FetchedEpisode { Number = n, Runtime = duration })
                        .ToList(),
                },
            };

        return new FetchedTitle
        {
            Kind = TitleKind.Anime,
            Format = format,
            AniListId = id,
            MalId = m.Int("idMal"),
            Name = title?.Str("english") ?? title?.Str("romaji"),
            OriginalName = title?.Str("native"),
            Year = m.Int("seasonYear") ?? start?.Int("year"),
            ReleaseDate = FullDate(start),
            Runtime = duration,
            Overview = CleanDescription(m.Str("description")),
            Status = MapStatus(m.Str("status")),
            PosterUrl = m.Prop("coverImage")?.Str("extraLarge") ?? m.Prop("coverImage")?.Str("large"),
            // AniList's banner is a wide strip, not a backdrop. TMDB fills the backdrop.
            BackdropUrl = null,
            Genres = m.Items("genres").Select(g => g.GetString()).OfType<string>().ToList(),
            Credits = credits,
            Seasons = seasons,
        }.WithSource(MetadataSource.AniList);
    }

    private async Task<JsonDocument> PostAsync(string query, object variables, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(Endpoint, new { query, variables }, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new MetadataException("Could not reach AniList. Check your internet connection.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new MetadataException("AniList is busy. Try again in a minute.");
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                throw new MetadataException($"AniList returned an error ({(int)response.StatusCode}).");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }

    /// <summary>"Director" and "Chief Director" count. Episode, animation and art directors do not.</summary>
    internal static bool IsDirectorRole(string? role) =>
        role is not null
        && (role == "Director" || role.StartsWith("Director (", StringComparison.Ordinal)
            || role == "Chief Director" || role.StartsWith("Chief Director (", StringComparison.Ordinal));

    internal static TitleFormat MapFormat(string? format) => format switch
    {
        "MOVIE" => TitleFormat.Film,
        "OVA" => TitleFormat.Ova,
        "ONA" => TitleFormat.Ona,
        "SPECIAL" or "MUSIC" => TitleFormat.Special,
        _ => TitleFormat.Series,
    };

    private static string? MapStatus(string? status) => status switch
    {
        "FINISHED" => "Finished",
        "RELEASING" => "Airing",
        "NOT_YET_RELEASED" => "Not yet released",
        "CANCELLED" => "Cancelled",
        "HIATUS" => "On hiatus",
        _ => null,
    };

    private static FetchedPerson Person(JsonElement node, JsonElement? birth) => new()
    {
        Name = node.Prop("name")?.Str("full") ?? "",
        AniListId = node.Int("id"),
        PhotoUrl = DefaultImage(node.Prop("image")?.Str("large")),
        BirthDate = FullDate(birth),
    };

    // AniList returns a generic placeholder when a person has no photo. Treat it as no photo.
    private static string? DefaultImage(string? url) =>
        url is null || url.Contains("/default.", StringComparison.Ordinal) ? null : url;

    private static DateOnly? FullDate(JsonElement? d) =>
        d?.Int("year") is { } y && d?.Int("month") is { } m && d?.Int("day") is { } day ? new DateOnly(y, m, day) : null;

    /// <summary>AniList descriptions contain light HTML even when asked for plain text.</summary>
    internal static string? CleanDescription(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var noTags = TagRegex().Replace(text.Replace("<br>", "\n").Replace("<br />", "\n"), "");
        var decoded = WebUtility.HtmlDecode(noTags);
        return BlankLinesRegex().Replace(decoded, "\n\n").Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLinesRegex();
}
