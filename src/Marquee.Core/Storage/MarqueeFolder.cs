namespace Marquee.Core.Storage;

/// <summary>
/// The portable Marquee folder. Everything the app writes lives inside it.
/// </summary>
public sealed class MarqueeFolder
{
    public const string DatabaseFileName = "marquee.db";

    private MarqueeFolder(string root)
    {
        Root = root;
        Data = Path.Combine(root, "data");
        Images = Path.Combine(Data, "images");
        Backups = Path.Combine(root, "backups");
        Exports = Path.Combine(root, "exports");
        Database = Path.Combine(Data, DatabaseFileName);
    }

    public string Root { get; }
    public string Data { get; }
    public string Images { get; }
    public string Backups { get; }
    public string Exports { get; }
    public string Database { get; }

    /// <summary>Finds the Marquee folder for the running app, based on where the executable lives.</summary>
    public static MarqueeFolder ForCurrentApp() => FromExecutableDirectory(AppContext.BaseDirectory);

    /// <summary>
    /// Works out the Marquee folder from the directory that holds the executable.
    /// On macOS the executable sits inside "Marquee.app/Contents/MacOS", so the
    /// folder that holds the .app bundle is used instead.
    /// </summary>
    public static MarqueeFolder FromExecutableDirectory(string executableDirectory)
    {
        var dir = Normalize(executableDirectory);
        return new MarqueeFolder(FindAppBundleParent(dir) ?? dir);
    }

    /// <summary>Uses an exact root folder. Handy for tests.</summary>
    public static MarqueeFolder AtRoot(string root) => new(Normalize(root));

    /// <summary>Creates the data, images, backups and exports folders if they are missing.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Images);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(Exports);
    }

    /// <summary>True when the path is the Marquee folder or anywhere inside it.</summary>
    public bool Contains(string path)
    {
        var full = Normalize(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return full.Equals(Root, comparison)
            || full.StartsWith(Root + Path.DirectorySeparatorChar, comparison)
            || full.StartsWith(Root + Path.AltDirectorySeparatorChar, comparison);
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    // Looks for ".../Name.app/Contents/MacOS" and returns the folder that holds Name.app.
    private static string? FindAppBundleParent(string dir)
    {
        var macOs = new DirectoryInfo(dir);
        var contents = macOs.Parent;
        var bundle = contents?.Parent;
        var isBundle = macOs.Name == "MacOS"
            && contents?.Name == "Contents"
            && bundle is not null
            && bundle.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
        return isBundle ? bundle!.Parent?.FullName : null;
    }
}
