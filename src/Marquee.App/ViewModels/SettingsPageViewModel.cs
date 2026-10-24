using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>Folder locations, the TMDB key, the version and attribution.</summary>
public sealed partial class SettingsPageViewModel : PageViewModel
{
    /// <summary>What must be typed to enable the erase button.</summary>
    public const string EraseWord = "ERASE";

    private readonly AppServices _services;
    private readonly SettingsStore _settings;
    private readonly Action<bool> _onErased;

    /// <param name="onErased">Called after erasing; true when settings were erased too and setup should run again.</param>
    public SettingsPageViewModel(AppServices services, Action<bool> onErased)
    {
        _services = services;
        _settings = services.Settings;
        _onErased = onErased;
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
        EraseConfirm = "";
        EraseError = null;
    }

    // ----- Erase all data -----

    [ObservableProperty]
    public partial bool BackupBeforeErase { get; set; } = true;

    [ObservableProperty]
    public partial bool EraseSettingsToo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EraseCommand))]
    public partial string EraseConfirm { get; set; } = "";

    [ObservableProperty]
    public partial string? EraseStatus { get; set; }

    [ObservableProperty]
    public partial string? EraseError { get; set; }

    private bool CanErase() => string.Equals(EraseConfirm.Trim(), EraseWord, StringComparison.Ordinal);

    [RelayCommand(CanExecute = nameof(CanErase))]
    private void Erase()
    {
        try
        {
            var backup = _services.Reset.EraseAll(BackupBeforeErase, EraseSettingsToo);
            _services.Images.Clear();
            EraseConfirm = "";
            EraseError = null;
            EraseStatus = backup is null
                ? "Everything was erased."
                : $"Everything was erased. A backup copy was saved to {backup}";
            _onErased(EraseSettingsToo);
        }
        catch (Exception ex)
        {
            EraseError = "Could not erase: " + ex.Message;
        }
    }

    [RelayCommand]
    private void SaveKey()
    {
        var key = TmdbKey.Trim();
        _settings.Set(SettingsStore.TmdbApiKey, key.Length == 0 ? null : key);
        KeyStatus = key.Length == 0 ? "Key removed. Search will use AniList only." : "Saved.";
    }
}
