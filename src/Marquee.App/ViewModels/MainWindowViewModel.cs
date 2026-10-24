using CommunityToolkit.Mvvm.ComponentModel;

namespace Marquee.App.ViewModels;

/// <summary>Chooses between the first-run screen and the main shell.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public MainWindowViewModel(AppServices services)
    {
        _services = services;
        Current = services.Settings.IsSetupCompleted
            ? new ShellViewModel(services)
            : new SetupViewModel(services.Folder, services.Settings, OpenShell);
    }

    [ObservableProperty]
    public partial ViewModelBase Current { get; set; }

    private void OpenShell() => Current = new ShellViewModel(_services);
}
