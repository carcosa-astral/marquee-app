using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>Folder locations, the TMDB key, the version and attribution.</summary>
public sealed partial class SettingsPageViewModel : PageViewModel
{
    private readonly SettingsStore _settings;

    public SettingsPageViewModel(AppServices services)
    {
        _settings = services.Settings;
        var folder = services.Folder;
        Details =
        [
            new("Marquee folder", folder.Root),
            new("Diary", folder.Database),
            new("Images", folder.Images),
            new("Backups", folder.Backups),
            new("Exports", folder.Exports),
            new("Version", typeof(SettingsPageViewModel).Assembly.GetName().Version?.ToString(3) ?? ""),
        ];
    }

    public IReadOnlyList<DetailRow> Details { get; }

    public string Attribution => "This product uses the TMDB API but is not endorsed or certified by TMDB. Anime data from AniList.";

    [ObservableProperty]
    public partial string TmdbKey { get; set; } = "";

    [ObservableProperty]
    public partial string? KeyStatus { get; set; }

    public override void OnShown()
    {
        TmdbKey = _settings.Get(SettingsStore.TmdbApiKey) ?? "";
        KeyStatus = null;
    }

    [RelayCommand]
    private void SaveKey()
    {
        var key = TmdbKey.Trim();
        _settings.Set(SettingsStore.TmdbApiKey, key.Length == 0 ? null : key);
        KeyStatus = key.Length == 0 ? "Key removed. Search will use AniList only." : "Saved.";
    }
}
