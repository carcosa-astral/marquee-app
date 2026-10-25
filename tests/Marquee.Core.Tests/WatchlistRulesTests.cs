using Marquee.Core.Model;
using Marquee.Core.Rules;

namespace Marquee.Core.Tests;

public class WatchlistRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    [Fact]
    public void A_future_date_is_coming_soon_and_moves_to_out_now_on_the_day()
    {
        var release = new DateOnly(2026, 11, 14);

        Assert.True(WatchlistRules.IsComingSoon(release, "Post Production", Today));
        Assert.True(WatchlistRules.IsComingSoon(release, null, release.AddDays(-1)));
        Assert.False(WatchlistRules.IsComingSoon(release, null, release));
        Assert.False(WatchlistRules.IsComingSoon(release, "Post Production", release.AddDays(3)));
    }

    [Theory]
    [InlineData("In Production", true)]
    [InlineData("Not yet released", true)]
    [InlineData("Planned", true)]
    [InlineData("Released", false)]
    [InlineData("Returning Series", false)]
    [InlineData(null, false)]
    public void Without_a_date_the_status_decides(string? status, bool comingSoon) =>
        Assert.Equal(comingSoon, WatchlistRules.IsComingSoon(null, status, Today));

    [Fact]
    public void The_titles_own_date_wins_over_the_one_typed_by_the_user()
    {
        var own = new DateOnly(2026, 12, 2);
        var typed = new DateOnly(2027, 1, 9);

        Assert.Equal(own, WatchlistRules.ReleaseDate(own, typed));
        Assert.Equal(typed, WatchlistRules.ReleaseDate(null, typed));
    }

    [Theory]
    [InlineData(21, "in 21 days")]
    [InlineData(1, "Tomorrow")]
    [InlineData(0, "Today")]
    public void Countdown_reads_naturally(int days, string text) =>
        Assert.Equal(text, WatchlistRules.CountdownText(Today.AddDays(days), Today));

    [Fact]
    public void Unknown_dates_say_unconfirmed_and_ago_counts_days()
    {
        Assert.Equal("Date unconfirmed", WatchlistRules.CountdownText(null, Today));
        Assert.Equal("71 days ago", WatchlistRules.AgoText(Today.AddDays(-71), Today));
        Assert.Equal("Yesterday", WatchlistRules.AgoText(Today.AddDays(-1), Today));
    }

    [Fact]
    public void Last_watched_is_the_furthest_main_episode()
    {
        var s1 = new Season { Number = 1, Episodes = [new() { Number = 1 }, new() { Number = 2 }] };
        var s2 = new Season { Number = 2, Episodes = [new() { Number = 1 }, new() { Number = 2 }] };
        var specials = new Season { Number = 0, Episodes = [new() { Number = 1 }] };
        var watched = new HashSet<string> { s1.Episodes[0].Id, s2.Episodes[0].Id, specials.Episodes[0].Id };

        Assert.Same(s2.Episodes[0], SeriesProgress.LastWatched([specials, s1, s2], watched));
        Assert.Null(SeriesProgress.LastWatched([s1], new HashSet<string>()));
    }
}
