using Marquee.Core.Rules;

namespace Marquee.Core.Tests;

public class WatchRulesTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(0.5, true)]
    [InlineData(4.5, true)]
    [InlineData(5.0, true)]
    [InlineData(0.0, false)]
    [InlineData(4.3, false)]
    [InlineData(5.5, false)]
    public void Ratings_are_half_steps_from_half_to_five(double? value, bool valid) =>
        Assert.Equal(valid, Rating.IsValid(value));

    [Theory]
    [InlineData(4.3, 4.5)]
    [InlineData(4.2, 4.0)]
    [InlineData(0.1, 0.5)]
    [InlineData(9, 5)]
    public void Snap_rounds_to_the_nearest_half_star(double input, double expected) =>
        Assert.Equal(expected, Rating.Snap(input));

    [Theory]
    [InlineData("7:30 PM", 19, 30)]
    [InlineData("7:30pm", 19, 30)]
    [InlineData("7pm", 19, 0)]
    [InlineData("7 PM", 19, 0)]
    [InlineData("19:30", 19, 30)]
    [InlineData("09:05", 9, 5)]
    [InlineData("12:15 am", 0, 15)]
    public void Start_times_accept_common_formats(string text, int hour, int minute)
    {
        Assert.True(TimeInput.TryParse(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_start_time_means_none(string? text)
    {
        Assert.True(TimeInput.TryParse(text, out var time));
        Assert.Null(time);
    }

    [Theory]
    [InlineData("late")]
    [InlineData("25:00")]
    [InlineData("7:75 PM")]
    public void Nonsense_times_are_rejected(string text) => Assert.False(TimeInput.TryParse(text, out _));

    [Fact]
    public void Times_show_in_twelve_hour_style() => Assert.Equal("7:30 PM", TimeInput.Format(new TimeOnly(19, 30)));

    private sealed record Ep(int Number);

    [Fact]
    public void Next_episode_follows_the_furthest_watched()
    {
        var eps = Enumerable.Range(1, 5).Select(n => new Ep(n)).ToList();

        Assert.Equal(1, WatchRules.NextEpisode(eps, _ => false)!.Number);
        Assert.Equal(4, WatchRules.NextEpisode(eps, e => e.Number is 1 or 3)!.Number);
        Assert.Null(WatchRules.NextEpisode(eps, e => e.Number == 5));
        Assert.Null(WatchRules.NextEpisode(new List<Ep>(), _ => false));
    }
}
