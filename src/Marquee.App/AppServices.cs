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

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var version = typeof(AppServices).Assembly.GetName().Version?.ToString(3) ?? "0";
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Marquee", version));

        Metadata = new MetadataService(
            new TmdbClient(http, () => Settings.Get(SettingsStore.TmdbApiKey)),
            new AniListClient(http));
        Downloader = new ImageDownloader(http, database.Folder);
        Images = new ImageLoader(http, database.Folder);
    }

    public MarqueeFolder Folder => Database.Folder;
    public MarqueeDatabase Database { get; }
    public SettingsStore Settings { get; }
    public TitleStore Titles { get; }
    public WatchStore Watches { get; }
    public TheaterStore Theaters { get; }
    public CompanionStore Companions { get; }
    public ResetService Reset { get; }
    public MetadataService Metadata { get; }
    public ImageDownloader Downloader { get; }
    public ImageLoader Images { get; }
}
