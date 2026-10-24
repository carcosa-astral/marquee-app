using System.Globalization;
using System.Text.Json;

namespace Marquee.Metadata;

/// <summary>Small helpers for reading optional values out of API responses.</summary>
internal static class Json
{
    public static JsonElement? Prop(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
            ? v
            : null;

    public static string? Str(this JsonElement e, string name)
    {
        var v = e.Prop(name);
        return v?.ValueKind == JsonValueKind.String && v.Value.GetString() is { Length: > 0 } s ? s : null;
    }

    public static int? Int(this JsonElement e, string name)
    {
        var v = e.Prop(name);
        return v?.ValueKind == JsonValueKind.Number && v.Value.TryGetInt32(out var i) ? i : null;
    }

    public static IEnumerable<JsonElement> Items(this JsonElement e, string name)
    {
        var v = e.Prop(name);
        return v?.ValueKind == JsonValueKind.Array ? v.Value.EnumerateArray() : [];
    }

    public static IEnumerable<JsonElement> Items(this JsonElement? e, string name) =>
        e is null ? [] : e.Value.Items(name);

    /// <summary>Parses "2006-11-25". Returns null for blank or partial dates.</summary>
    public static DateOnly? Date(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static int? Year(string? text) => Date(text)?.Year
        ?? (text is { Length: >= 4 } && int.TryParse(text[..4], out var y) ? y : null);
}
