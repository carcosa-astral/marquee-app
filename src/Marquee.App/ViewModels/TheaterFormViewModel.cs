using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Model;
using Marquee.Data;

namespace Marquee.App.ViewModels;

/// <summary>Add or edit a theater. Matches the "Add a theater" panel in the LogWatch mock.</summary>
public sealed partial class TheaterFormViewModel : ViewModelBase
{
    private readonly TheaterStore _store;
    private readonly string? _theaterId;
    private readonly Action<Theater?> _done;

    public TheaterFormViewModel(TheaterStore store, Theater? existing, Action<Theater?> done, string? suggestedName = null)
    {
        _store = store;
        _theaterId = existing?.Id;
        _done = done;
        Name = existing?.Name ?? suggestedName ?? "";
        City = existing?.City ?? "";
        Chain = existing?.Chain ?? "";

        var selected = existing?.Formats ?? [];
        foreach (var format in TheaterStore.CommonFormats.Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase))
            Formats.Add(new FormatChoice(format, selected.Contains(format, StringComparer.OrdinalIgnoreCase)));
    }

    public bool IsEditing => _theaterId is not null;
    public string Kicker => IsEditing ? "EDIT THEATER" : "NEW THEATER";
    public string Heading => IsEditing ? "Edit theater" : "Add a theater";
    public string SaveLabel => IsEditing ? "Save changes" : "Add theater";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string City { get; set; }

    [ObservableProperty]
    public partial string Chain { get; set; }

    [ObservableProperty]
    public partial string NewFormat { get; set; } = "";

    [ObservableProperty]
    public partial bool IsAddingFormat { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    public ObservableCollection<FormatChoice> Formats { get; } = [];

    [RelayCommand]
    private void StartAddingFormat() => IsAddingFormat = true;

    [RelayCommand]
    private void AddFormat()
    {
        var name = NewFormat.Trim();
        if (name.Length > 0)
        {
            var existing = Formats.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                Formats.Add(new FormatChoice(name, true));
            else
                existing.IsSelected = true;
        }
        NewFormat = "";
        IsAddingFormat = false;
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        try
        {
            var theater = _store.Save(_theaterId, Name, City, Chain, Formats.Where(f => f.IsSelected).Select(f => f.Name));
            _done(theater);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => _done(null);
}

/// <summary>One format that can be switched on or off.</summary>
public sealed partial class FormatChoice(string name, bool selected) : ViewModelBase
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = selected;

    [RelayCommand]
    private void Toggle() => IsSelected = !IsSelected;
}
