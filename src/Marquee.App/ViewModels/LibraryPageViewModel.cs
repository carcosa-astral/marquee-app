using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Marquee.Core.Model;

namespace Marquee.App.ViewModels;

/// <summary>A poster grid of every Movie, TV show or anime in the library. Filters arrive in 0.9.0.</summary>
public sealed partial class LibraryPageViewModel(AppServices services, TitleKind kind, string title, string emptyText) : PageViewModel
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
            var item = new LibraryItemViewModel(t);
            Items.Add(item);
            item.LoadPoster(services.Images);
        }
        IsEmpty = Items.Count == 0;
        CountText = Items.Count == 1 ? "1 TITLE" : $"{Items.Count} TITLES";
    }
}

public sealed partial class LibraryItemViewModel(Title title) : ViewModelBase
{
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
