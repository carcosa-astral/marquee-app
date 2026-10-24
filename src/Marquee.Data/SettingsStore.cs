using Marquee.Core.Model;

namespace Marquee.Data;

/// <summary>Reads and writes app settings. They live in the diary file, never outside the Marquee folder.</summary>
public sealed class SettingsStore(MarqueeDatabase database)
{
    public const string SetupCompleted = "setup.completed";
    public const string TmdbApiKey = "tmdb.apiKey";

    public string? Get(string key)
    {
        using var db = database.CreateContext();
        return db.Settings.Find(key)?.Value;
    }

    public void Set(string key, string? value)
    {
        using var db = database.CreateContext();
        var setting = db.Settings.Find(key);
        if (setting is null)
            db.Settings.Add(new Setting { Key = key, Value = value });
        else
            setting.Value = value;
        db.SaveChanges();
    }

    public bool IsSetupCompleted => Get(SetupCompleted) == "true";
}
