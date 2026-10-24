namespace Marquee.App.ViewModels;

/// <summary>A page shown in the main area of the shell.</summary>
public abstract class PageViewModel : ViewModelBase
{
    /// <summary>Called each time the page is opened, so it can show fresh data.</summary>
    public virtual void OnShown() { }
}

/// <summary>
/// A simple page for sections that arrive in a later milestone: a heading,
/// a short description and a card saying when.
/// </summary>
public sealed class PlaceholderPageViewModel : PageViewModel
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string CardTitle { get; init; }
    public required string CardText { get; init; }
}

public sealed record DetailRow(string Label, string Value);

public static class Pages
{
    public static PlaceholderPageViewModel Home() => new()
    {
        Title = "Home",
        Description = "Continue watching, what is in progress, and your year so far.",
        CardTitle = "Start by logging a watch",
        CardText = "Use the search box at the top (Ctrl K) to find a title, then Log a watch. Your entries show in the Diary. This page fills up in a later milestone.",
    };

    public static PlaceholderPageViewModel Soon(string title, string description, string milestone) => new()
    {
        Title = title,
        Description = description,
        CardTitle = $"Coming in {milestone}",
        CardText = "This page is part of a later milestone in the build plan.",
    };
}
