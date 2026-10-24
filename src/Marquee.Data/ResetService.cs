using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Marquee.Data;

/// <summary>Erases the diary to start over, optionally after saving a backup copy.</summary>
public sealed class ResetService(MarqueeDatabase database)
{
    /// <summary>
    /// Deletes every diary entry, title, person, theater, companion and saved image.
    /// Settings (such as the TMDB key) are kept unless <paramref name="includeSettings"/> is true.
    /// Returns the backup file's path when one was made.
    /// </summary>
    public string? EraseAll(bool backupFirst, bool includeSettings)
    {
        var backup = backupFirst ? Backup("before-erase") : null;

        using (var db = database.CreateContext())
        {
            using (var tx = db.Database.BeginTransaction())
            {
                // Children first, so foreign keys are never in the way.
                db.WatchCompanions.ExecuteDelete();
                db.Watches.ExecuteDelete();
                db.Watchlist.ExecuteDelete();
                db.OnIce.ExecuteDelete();
                db.FieldOverrides.ExecuteDelete();
                db.FieldSources.ExecuteDelete();
                db.Credits.ExecuteDelete();
                db.TitleGenres.ExecuteDelete();
                db.Episodes.ExecuteDelete();
                db.Seasons.ExecuteDelete();
                db.Titles.ExecuteDelete();
                db.People.ExecuteDelete();
                db.Genres.ExecuteDelete();
                db.Theaters.ExecuteDelete();
                db.Companions.ExecuteDelete();
                if (includeSettings)
                    db.Settings.ExecuteDelete();
                tx.Commit();
            }
            // Rewrites the file so erased data is really gone, not just marked free.
            db.Database.ExecuteSqlRaw("VACUUM;");
        }
        SqliteConnection.ClearAllPools();

        var images = database.Folder.Images;
        if (database.Folder.Contains(images) && Directory.Exists(images))
            Directory.Delete(images, recursive: true);
        Directory.CreateDirectory(images);
        return backup;
    }

    /// <summary>Copies the diary file into backups/ and returns the copy's path.</summary>
    public string Backup(string label)
    {
        var folder = database.Folder;
        Directory.CreateDirectory(folder.Backups);
        var path = Path.Combine(folder.Backups, $"marquee-{label}-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        using (var source = new SqliteConnection($"Data Source={folder.Database}"))
        using (var target = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }
        return path;
    }
}
