using CommunityToolkit.Mvvm.ComponentModel;

namespace Marquee.App.ViewModels;

/// <summary>Chooses between the first-run screen and the main shell.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public MainWindowViewModel(AppServices services)
    {
        _services = services;
        Current = services.Settings.IsSetupCompleted ? NewShell() : NewSetup();
    }

    [ObservableProperty]
    public partial ViewModelBase Current { get; set; }

    private ShellViewModel NewShell() => new(_services, () => Current = NewSetup());

    private SetupViewModel NewSetup() => new(_services.Folder, _services.Settings, () => Current = NewShell());
}
