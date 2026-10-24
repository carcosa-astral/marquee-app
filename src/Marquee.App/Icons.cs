using Avalonia.Media;

namespace Marquee.App;

/// <summary>Line icons from the design mocks, drawn in a 24 by 24 box.</summary>
public static class Icons
{
    public static readonly Geometry Logo = P("M3 6h18v12H3zM7.5 10h.01M12 10h.01M16.5 10h.01M7.5 14h.01M12 14h.01M16.5 14h.01");
    public static readonly Geometry Home = P("M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z");
    public static readonly Geometry Diary = P("M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3zM5 17a3 3 0 0 1 3-3h11");
    public static readonly Geometry Watchlist = P("M7 4h10a1 1 0 0 1 1 1v15l-6-4-6 4V5a1 1 0 0 1 1-1z");
    public static readonly Geometry OnIce = P("M12 3v18M4.2 7.5l15.6 9M4.2 16.5l15.6-9");
    public static readonly Geometry People = P("M9 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM2.5 20c0-3.6 2.9-6 6.5-6s6.5 2.4 6.5 6M16 4.3a3.5 3.5 0 0 1 0 6.4M18 14.3c2 .7 3.5 2.4 3.5 5.7");
    public static readonly Geometry Recap = P("M5 20V11M12 20V4M19 20v-6");
    public static readonly Geometry Movies = P("M4 5h16v14H4zM8 5v14M16 5v14M4 9h4M4 15h4M16 9h4M16 15h4");
    public static readonly Geometry Tv = P("M4 7h16v11H4zM9 3l3 4 3-4");
    public static readonly Geometry Anime = P("M12 3l2.2 6.3L21 12l-6.8 2.7L12 21l-2.2-6.3L3 12l6.8-2.7z");
    public static readonly Geometry Theaters = P("M4 7h16v3a2 2 0 0 0 0 4v3H4v-3a2 2 0 0 0 0-4zM14 7v2M14 11v2M14 15v2");
    public static readonly Geometry Export = P("M12 4v11M7 10l5 5 5-5M5 20h14");
    public static readonly Geometry Settings = P("M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19 12a7 7 0 0 0-.1-1.2l2-1.5-2-3.4-2.3.9a7 7 0 0 0-2-1.2L14.2 3h-4l-.4 2.6a7 7 0 0 0-2 1.2l-2.3-.9-2 3.4 2 1.5A7 7 0 0 0 5 12c0 .4 0 .8.1 1.2l-2 1.5 2 3.4 2.3-.9c.6.5 1.3.9 2 1.2l.4 2.6h4l.4-2.6c.7-.3 1.4-.7 2-1.2l2.3.9 2-3.4-2-1.5c.1-.4.1-.8.1-1.2z");
    public static readonly Geometry Search = P("M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM21 21l-4.3-4.3");
    public static readonly Geometry Plus = P("M12 5v14M5 12h14");
    public static readonly Geometry Check = P("M5 12l5 5L20 7");

    private static Geometry P(string data) => StreamGeometry.Parse(data);
}
