namespace Marquee.App.ViewModels;

/// <summary>Shown when the Marquee folder or diary file cannot be opened.</summary>
public sealed class StartupErrorViewModel(string folder, string message) : ViewModelBase
{
    public string Folder { get; } = folder;
    public string Message { get; } = message;
}
