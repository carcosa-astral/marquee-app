using Marquee.Core.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Marquee.Data;

public class MarqueeDbContext(DbContextOptions<MarqueeDbContext> options) : DbContext(options)
{
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Credit> Credits => Set<Credit>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<TitleGenre> TitleGenres => Set<TitleGenre>();
    public DbSet<Watch> Watches => Set<Watch>();
    public DbSet<Companion> Companions => Set<Companion>();
    public DbSet<WatchCompanion> WatchCompanions => Set<WatchCompanion>();
    public DbSet<Theater> Theaters => Set<Theater>();
    public DbSet<WatchlistItem> Watchlist => Set<WatchlistItem>();
    public DbSet<IceItem> OnIce => Set<IceItem>();
    public DbSet<FieldOverride> FieldOverrides => Set<FieldOverride>();
    public DbSet<FieldSource> FieldSources => Set<FieldSource>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Store enums as lowercase text ("movie", "anime", "theater") so the file stays readable.
        builder.Properties<TitleKind>().HaveConversion<LowercaseEnumConverter<TitleKind>>();
        builder.Properties<TitleFormat>().HaveConversion<LowercaseEnumConverter<TitleFormat>>();
        builder.Properties<CreditRole>().HaveConversion<LowercaseEnumConverter<CreditRole>>();
        builder.Properties<WatchPlace>().HaveConversion<LowercaseEnumConverter<WatchPlace>>();
        builder.Properties<IceState>().HaveConversion<LowercaseEnumConverter<IceState>>();
        builder.Properties<MetadataSource>().HaveConversion<LowercaseEnumConverter<MetadataSource>>();
        builder.Properties<TmdbType>().HaveConversion<LowercaseEnumConverter<TmdbType>>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Title>(e =>
        {
            e.HasIndex(t => new { t.TmdbType, t.TmdbId });
            e.HasIndex(t => t.AniListId);
            e.HasIndex(t => t.MalId);
            e.HasIndex(t => t.Name);
        });

        b.Entity<Season>().HasIndex(s => new { s.TitleId, s.Number });
        b.Entity<Episode>().HasIndex(ep => new { ep.SeasonId, ep.Number });

        b.Entity<Person>(e =>
        {
            e.HasIndex(p => p.TmdbId);
            e.HasIndex(p => p.AniListId);
            e.HasIndex(p => p.Name);
        });

        b.Entity<Credit>(e =>
        {
            e.HasIndex(c => c.PersonId);
            e.HasIndex(c => c.TitleId);
        });

        b.Entity<Genre>().HasIndex(g => g.Name).IsUnique();
        b.Entity<TitleGenre>().HasKey(tg => new { tg.TitleId, tg.GenreId });

        b.Entity<Watch>(e =>
        {
            e.HasIndex(w => w.WatchedOn);
            e.HasIndex(w => w.TitleId);
            // Watches are the user's history. Never let deleting metadata take them with it.
            e.HasOne(w => w.Title).WithMany().HasForeignKey(w => w.TitleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(w => w.Episode).WithMany().HasForeignKey(w => w.EpisodeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(w => w.Theater).WithMany().HasForeignKey(w => w.TheaterId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<WatchCompanion>().HasKey(wc => new { wc.WatchId, wc.CompanionId });

        b.Entity<WatchlistItem>().HasIndex(w => w.TitleId).IsUnique();
        b.Entity<IceItem>().HasIndex(i => i.TitleId).IsUnique();
        b.Entity<FieldOverride>().HasIndex(o => new { o.TitleId, o.FieldName }).IsUnique();
        b.Entity<FieldSource>().HasIndex(s => new { s.TitleId, s.FieldName }).IsUnique();

        b.Entity<Setting>().HasKey(s => s.Key);
    }
}

/// <summary>Saves an enum as its lowercase name, for example TitleKind.Anime as "anime".</summary>
public class LowercaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    v => v.ToString().ToLowerInvariant(),
    v => Enum.Parse<TEnum>(v, true))
    where TEnum : struct, Enum;
