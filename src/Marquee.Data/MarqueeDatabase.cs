using Marquee.Core.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Marquee.Data;

/// <summary>Opens the diary file inside the Marquee folder and keeps its schema up to date.</summary>
public sealed class MarqueeDatabase(MarqueeFolder folder)
{
    public MarqueeFolder Folder { get; } = folder;

    public MarqueeDbContext CreateContext() => new(Options(Folder.Database));

    /// <summary>Creates the folders and the database if needed, then applies any pending migrations.</summary>
    public void Initialize()
    {
        Folder.EnsureCreated();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    internal static DbContextOptions<MarqueeDbContext> Options(string databasePath) =>
        new DbContextOptionsBuilder<MarqueeDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
}

/// <summary>Used only by "dotnet ef" when creating migrations. Never runs in the app.</summary>
public sealed class DesignTimeContextFactory : IDesignTimeDbContextFactory<MarqueeDbContext>
{
    public MarqueeDbContext CreateDbContext(string[] args) => new(MarqueeDatabase.Options("design-time.db"));
}
