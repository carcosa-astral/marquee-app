using System.Globalization;
using Marquee.Core.Model;

namespace Marquee.Core.Metadata;

/// <summary>
/// Reads and writes the editable Title fields as text, for overrides.
/// Overrides are stored as text so one table can hold every field.
/// </summary>
public static class TitleFieldValues
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [TitleFields.Name] = "Title",
        [TitleFields.OriginalName] = "Original title",
        [TitleFields.Year] = "Year",
        [TitleFields.Runtime] = "Runtime",
        [TitleFields.ReleaseDate] = "Release date",
        [TitleFields.Status] = "Status",
        [TitleFields.Overview] = "Overview",
    };

    /// <summary>Fields that can be edited by hand. Images are changed through Fix match or Refresh.</summary>
    public static IReadOnlyList<string> Editable { get; } =
    [
        TitleFields.Name, TitleFields.OriginalName, TitleFields.Year, TitleFields.Runtime,
        TitleFields.ReleaseDate, TitleFields.Status, TitleFields.Overview,
    ];

    public static string? Get(Title t, string field) => field switch
    {
        TitleFields.Name => t.Name,
        TitleFields.OriginalName => t.OriginalName,
        TitleFields.Year => t.Year?.ToString(CultureInfo.InvariantCulture),
        TitleFields.Runtime => t.RuntimeMinutes?.ToString(CultureInfo.InvariantCulture),
        TitleFields.ReleaseDate => t.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TitleFields.Status => t.Status,
        TitleFields.Overview => t.Overview,
        TitleFields.Poster => t.PosterPath,
        TitleFields.Backdrop => t.BackdropPath,
        _ => null,
    };

    /// <summary>Checks a typed value. Returns an error message, or null when it is fine.</summary>
    public static string? Validate(string field, string? value)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v))
            return field == TitleFields.Name ? "A title is needed." : null;
        return field switch
        {
            TitleFields.Year when !int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var y) || y < 1870 || y > 2100
                => "Use a year like 1995.",
            TitleFields.Runtime when !int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var m) || m < 1 || m > 2000
                => "Use minutes, like 170.",
            TitleFields.ReleaseDate when !DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                => "Use a date like 1995-12-15.",
            _ => null,
        };
    }

    /// <summary>Writes a value that already passed <see cref="Validate"/>. Blank clears the field (except the title).</summary>
    public static void Set(Title t, string field, string? value)
    {
        var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        switch (field)
        {
            case TitleFields.Name: t.Name = v ?? t.Name; break;
            case TitleFields.OriginalName: t.OriginalName = v; break;
            case TitleFields.Year: t.Year = v is null ? null : int.Parse(v, CultureInfo.InvariantCulture); break;
            case TitleFields.Runtime: t.RuntimeMinutes = v is null ? null : int.Parse(v, CultureInfo.InvariantCulture); break;
            case TitleFields.ReleaseDate: t.ReleaseDate = v is null ? null : DateOnly.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture); break;
            case TitleFields.Status: t.Status = v; break;
            case TitleFields.Overview: t.Overview = v; break;
            case TitleFields.Poster: t.PosterPath = v; break;
            case TitleFields.Backdrop: t.BackdropPath = v; break;
        }
    }
}
