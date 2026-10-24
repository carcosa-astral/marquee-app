using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Marquee.Core.Model;

namespace Marquee.App.ViewModels;

/// <summary>A poster grid of every movie, TV show or anime you have watched. Filters arrive in 0.9.0.</summary>
public sealed partial class LibraryPageViewModel(AppServices services, TitleKind kind, Action<string> logWatch, string title, string emptyText) : PageViewModel
{
    public string Title { get; } = title;
    public string EmptyText { get; } = emptyText;

    public ObservableCollection<LibraryItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    public override void OnShown()
    {
        Items.Clear();
        foreach (var t in services.Titles.List(kind))
        {
            var item = new LibraryItemViewModel(t, logWatch);
            Items.Add(item);
            item.LoadPoster(services.Images);
        }
        IsEmpty = Items.Count == 0;
        CountText = $"{Items.Count:N0} WATCHED";
    }
}

public sealed partial class LibraryItemViewModel(Title title, Action<string> logWatch) : ViewModelBase
{
    /// <summary>Until title pages arrive in 0.4.0, clicking a poster logs a watch.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Open() => logWatch(title.Id);

    public string Name { get; } = title.Name;

    public string Details { get; } = string.Join(" · ", new[]
    {
        title.Year?.ToString(),
        title.RuntimeMinutes is > 0 ? $"{title.RuntimeMinutes} min" : null,
    }.OfType<string>());

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    public async void LoadPoster(ImageLoader images) => Poster = await images.LoadSavedAsync(title.PosterPath, 300);
}
