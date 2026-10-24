namespace Marquee.Core.Metadata;

/// <summary>Which credits count toward a person page (build plan, section 6).</summary>
public static class CreditRules
{
    /// <summary>
    /// False for appearances as themselves and for archive footage, which never count.
    /// </summary>
    public static bool IsCountedCastRole(string? character)
    {
        if (string.IsNullOrWhiteSpace(character))
            return true;
        var c = character.Trim().ToLowerInvariant();
        if (c.Contains("archive footage") || c.Contains("uncredited archive"))
            return false;
        return c is not ("self" or "himself" or "herself" or "themselves")
            && !c.StartsWith("self ")
            && !c.StartsWith("self -")
            && !c.StartsWith("himself ")
            && !c.StartsWith("herself ")
            && !c.StartsWith("self (");
    }
}
