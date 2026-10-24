using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Storage;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>First run: confirm the folder, enter the TMDB key, start empty.</summary>
public partial class SetupViewModel(MarqueeFolder folder, SettingsStore settings, Action onFinished) : ViewModelBase
{
    public string FolderName { get; } = Path.GetFileName(folder.Root) + "/";
    public string FolderPath { get; } = folder.Root;

    [ObservableProperty]
    public partial string TmdbKey { get; set; } = settings.Get(SettingsStore.TmdbApiKey) ?? "";

    /// <summary>Importing a Marquee JSON arrives in 0.8.0. Starting empty is the only choice for now.</summary>
    public bool CanImport => false;

    [RelayCommand]
    private void Open()
    {
        var key = TmdbKey.Trim();
        settings.Set(SettingsStore.TmdbApiKey, key.Length == 0 ? null : key);
        settings.Set(SettingsStore.SetupCompleted, "true");
        onFinished();
    }
}
