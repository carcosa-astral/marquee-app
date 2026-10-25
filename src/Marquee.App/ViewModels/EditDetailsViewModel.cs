using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Marquee.Core.Metadata;
using Marquee.Core.Model;

namespace Marquee.App.ViewModels;

/// <summary>Edit a title's details by hand. Matches the "Edit details" panel in docs/design/mocks/EditMeta.dc.html.</summary>
public sealed partial class EditDetailsViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly string _titleId;
    private readonly Action<bool> _close;
    private bool _resetSomething;

    public EditDetailsViewModel(AppServices services, string titleId, Action<bool> close)
    {
        _services = services;
        _titleId = titleId;
        _close = close;
        Load();
    }

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string YearText { get; set; } = "";
    [ObservableProperty] public partial string EditedText { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }

    public ObservableCollection<EditField> Fields { get; } = [];

    private void Load()
    {
        var details = _services.Titles.GetDetails(_titleId)!;
        Name = details.Title.Name;
        YearText = details.Title.Year?.ToString() ?? "";
        Fields.Clear();
        foreach (var field in TitleFieldValues.Editable)
        {
            details.Sources.TryGetValue(field, out var source);
            Fields.Add(new EditField(
                field,
                TitleFieldValues.Labels[field],
                TitleFieldValues.Get(details.Title, field) ?? "",
                details.Sources.ContainsKey(field) ? source : null,
                Reset));
        }
        UpdateEditedText();
    }

    private void UpdateEditedText()
    {
        var n = Fields.Count(f => f.IsEdited);
        EditedText = n == 0 ? "No fields edited by you" : n == 1 ? "1 field edited by you" : $"{n} fields edited by you";
    }

    /// <summary>Resetting is saved right away, so the source's value shows at once.</summary>
    private void Reset(EditField field)
    {
        _services.Titles.ResetEdit(_titleId, field.Field);
        _resetSomething = true;
        var pending = Fields.Where(f => f.IsChanged && f != field).ToDictionary(f => f.Field, f => f.Value);
        Load();
        foreach (var f in Fields)
        {
            if (pending.TryGetValue(f.Field, out var v))
                f.Value = v;
        }
    }

    [RelayCommand]
    private void Save()
    {
        foreach (var f in Fields)
            f.Error = TitleFieldValues.Validate(f.Field, f.Value);
        if (Fields.Any(f => f.Error is not null))
        {
            Error = "Fix the fields marked in red first.";
            return;
        }
        var changed = Fields.Where(f => f.IsChanged).ToDictionary(f => f.Field, f => (string?)f.Value);
        try
        {
            if (changed.Count > 0)
                _services.Titles.SaveEdits(_titleId, changed);
            _close(changed.Count > 0 || _resetSomething);
        }
        catch (Exception ex)
        {
            Error = "Could not save: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => _close(_resetSomething);
}

public sealed partial class EditField : ViewModelBase
{
    private readonly string _original;
    private readonly Action<EditField> _reset;

    public EditField(string field, string label, string value, MetadataSource? source, Action<EditField> reset)
    {
        Field = field;
        Label = label;
        _original = value;
        Value = value;
        Source = source;
        _reset = reset;
    }

    public string Field { get; }
    public string Label { get; }
    public MetadataSource? Source { get; }
    public bool IsEdited => Source == MetadataSource.User;
    public bool IsMultiline => Field == TitleFields.Overview;
    public bool IsTmdb => Source == MetadataSource.Tmdb;
    public bool IsAniList => Source == MetadataSource.AniList;
    public string SourceText => Source switch
    {
        MetadataSource.Tmdb => "TMDB",
        MetadataSource.AniList => "AniList",
        MetadataSource.User => "You edited",
        _ => "Not set",
    };

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    public bool IsChanged => !string.Equals((Value ?? "").Trim(), _original.Trim(), StringComparison.Ordinal);

    [RelayCommand]
    private void Reset() => _reset(this);
}
