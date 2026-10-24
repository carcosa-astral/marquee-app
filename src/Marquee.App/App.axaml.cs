using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Marquee.App.ViewModels;
using Marquee.App.Views;
using Marquee.Core.Storage;
using Marquee.Data;

namespace Marquee.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = CreateMainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ViewModelBase CreateMainViewModel()
    {
        var folder = MarqueeFolder.ForCurrentApp();
        try
        {
            var database = new MarqueeDatabase(folder);
            database.Initialize();
            return new MainWindowViewModel(database, new SettingsStore(database));
        }
        catch (Exception ex)
        {
            return new StartupErrorViewModel(folder.Root, ex.Message);
        }
    }
}
