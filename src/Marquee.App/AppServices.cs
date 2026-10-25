using System.Net.Http.Headers;
using Marquee.Core.Storage;
using Marquee.Data;
using Marquee.Metadata;

namespace Marquee.App;

/// <summary>The shared pieces the view models use. Created once at startup.</summary>
public sealed class AppServices
{
    public AppServices(MarqueeDatabase database)
    {
        Database = database;
        Settings = new SettingsStore(database);
        Titles = new TitleStore(database);
        Watches = new WatchStore(database);
        Theaters = new TheaterStore(database);
        Companions = new CompanionStore(database);
        Reset = new ResetService(database);
        Watchlist = new WatchlistStore(database);
        Ice = new IceStore(database);

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var version = typeof(AppServices).Assembly.GetName().Version?.ToString(3) ?? "0";
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Marquee", version));

        Metadata = new MetadataService(
            new TmdbClient(http, () => Settings.Get(SettingsStore.TmdbApiKey)),
            new AniListClient(http));
        Downloader = new ImageDownloader(http, database.Folder);
        Images = new ImageLoader(http, database.Folder);
        Imdb = new ImdbRatingsFile(http, database.Folder);
    }

    /// <summary>
    /// Copies IMDb scores from the saved IMDb ratings file onto titles (one title, or all).
    /// Does nothing when the file has not been downloaded. Returns how many titles were updated.
    /// </summary>
    public async Task<int> ApplyImdbRatingsAsync(string? titleId = null)
    {
        if (!Imdb.Exists)
            return 0;
        var ids = Titles.ImdbIds(titleId);
        var scores = await Imdb.ReadAsync(ids);
        return Titles.SetImdbRatings(scores);
    }

    public MarqueeFolder Folder => Database.Folder;
    public MarqueeDatabase Database { get; }
    public SettingsStore Settings { get; }
    public TitleStore Titles { get; }
    public WatchStore Watches { get; }
    public TheaterStore Theaters { get; }
    public CompanionStore Companions { get; }
    public ResetService Reset { get; }
    public WatchlistStore Watchlist { get; }
    public IceStore Ice { get; }

    /// <summary>
    /// The saved id for a search result, fetching and saving the title (with its images) first
    /// when it is new. Used by the watchlist, which holds titles you have not watched yet.
    /// </summary>
    public async Task<string> EnsureSavedAsync(Core.Metadata.SearchResult result)
    {
        if (Titles.FindId(result) is { } id)
            return id;
        var fetched = await Metadata.FetchAsync(result);
        var images = await Downloader.SaveAllAsync(fetched);
        var saved = await Task.Run(() => Titles.Save(fetched, images));
        await ApplyImdbRatingsAsync(saved.Id);
        return saved.Id;
    }
    public MetadataService Metadata { get; }
    public ImageDownloader Downloader { get; }
    public ImageLoader Images { get; }
    public ImdbRatingsFile Imdb { get; }
}
