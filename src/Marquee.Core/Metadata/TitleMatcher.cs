using System.Globalization;
using System.Text;
using Marquee.Core.Model;

namespace Marquee.Core.Metadata;

/// <summary>
/// Decides whether results from two sources are the same title.
/// External ids win. Without one, the title, year and format must all agree.
/// </summary>
public static class TitleMatcher
{
    public static bool IsSameTitle(SearchHit a, SearchHit b)
    {
        if (a.MalId is not null && b.MalId is not null)
            return a.MalId == b.MalId;
        return a.Year is not null
            && a.Year == b.Year
            && FormatsCompatible(a.Format, b.Format)
            && SharesName(a.Names, b.Names);
    }

    /// <summary>A film matches a movie, and every episodic format matches a series.</summary>
    public static bool FormatsCompatible(TitleFormat a, TitleFormat b) => IsFilm(a) == IsFilm(b);

    private static bool IsFilm(TitleFormat f) => f is TitleFormat.Movie or TitleFormat.Film;

    public static bool SharesName(IEnumerable<string> a, IEnumerable<string> b)
    {
        var set = a.Select(Normalize).Where(n => n.Length > 0).ToHashSet();
        return b.Select(Normalize).Any(set.Contains);
    }

    /// <summary>Lowercase letters and digits only, with accents removed.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
