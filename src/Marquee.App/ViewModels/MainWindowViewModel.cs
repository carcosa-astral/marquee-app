using CommunityToolkit.Mvvm.ComponentModel;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>Chooses between the first-run screen and the main shell.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly MarqueeDatabase _database;
    private readonly SettingsStore _settings;

    public MainWindowViewModel(MarqueeDatabase database, SettingsStore settings)
    {
        _database = database;
        _settings = settings;
        Current = settings.IsSetupCompleted
            ? new ShellViewModel(database, settings)
            : new SetupViewModel(database.Folder, settings, OpenShell);
    }

    [ObservableProperty]
    public partial ViewModelBase Current { get; set; }

    private void OpenShell() => Current = new ShellViewModel(_database, _settings);
}
