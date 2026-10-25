using Marquee.Core.Metadata;
using Marquee.Core.Model;
using Marquee.Core.Rules;

namespace Marquee.Core.Tests;

public class TitlePageRulesTests
{
    private static List<Season> Show(params int[] episodesPerSeason)
    {
        var seasons = new List<Season> { new() { Number = 0, Episodes = [new() { Number = 1, RuntimeMinutes = 30 }] } };
        for (var i = 0; i < episodesPerSeason.Length; i++)
        {
            seasons.Add(new Season
            {
                Number = i + 1,
                Episodes = Enumerable.Range(1, episodesPerSeason[i]).Select(n => new Episode { Number = n }).ToList(),
            });
        }
        return seasons;
    }

    [Fact]
    public void Progress_counts_main_episodes_and_ignores_specials()
    {
        var seasons = Show(6, 6);
        var watched = seasons[1].Episodes.Select(e => e.Id).Append(seasons[0].Episodes[0].Id).ToHashSet();

        var p = SeriesProgress.For(seasons, watched, defaultRuntime: 47);

        Assert.Equal((6, 12, SeriesState.Watching), (p.Watched, p.Total, p.State));
        Assert.Same(seasons[2].Episodes[0], p.Next);
        Assert.Equal(6 * 47, p.MinutesLeft);
        Assert.Equal(0.5, p.Fraction);
    }

    [Fact]
    public void Nothing_watched_is_not_started_and_everything_watched_is_finished()
    {
        var seasons = Show(3);
        Assert.Equal(SeriesState.NotStarted, SeriesProgress.For(seasons, new HashSet<string>(), 24).State);

        var all = seasons[1].Episodes.Select(e => e.Id).ToHashSet();
        var done = SeriesProgress.For(seasons, all, 24);
        Assert.Equal(SeriesState.Finished, done.State);
        Assert.Null(done.Next);
        Assert.Equal(0, done.MinutesLeft);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(40, "About 40 minutes left")]
    [InlineData(60, "About 1 hour left")]
    [InlineData(94, "About 1.5 hours left")]
    [InlineData(264, "About 4.5 hours left")]
    [InlineData(360, "About 6 hours left")]
    public void Time_left_reads_naturally(int minutes, string text) => Assert.Equal(text, SeriesProgress.TimeLeftText(minutes));

    [Theory]
    [InlineData("year", "1995", null)]
    [InlineData("year", "95", "Use a year like 1995.")]
    [InlineData("year", "", null)]
    [InlineData("runtime", "170", null)]
    [InlineData("runtime", "two hours", "Use minutes, like 170.")]
    [InlineData("releaseDate", "1995-12-15", null)]
    [InlineData("releaseDate", "Dec 15 1995", "Use a date like 1995-12-15.")]
    [InlineData("name", " ", "A title is needed.")]
    [InlineData("overview", "", null)]
    public void Edited_values_are_checked(string field, string value, string? error) =>
        Assert.Equal(error, TitleFieldValues.Validate(field, value));

    [Fact]
    public void Field_values_round_trip_as_text()
    {
        var t = new Title { Name = "Heat" };
        TitleFieldValues.Set(t, TitleFields.Year, "1995");
        TitleFieldValues.Set(t, TitleFields.Runtime, "170");
        TitleFieldValues.Set(t, TitleFields.ReleaseDate, "1995-12-15");
        TitleFieldValues.Set(t, TitleFields.Name, "  ");

        Assert.Equal(("Heat", 1995, 170, new DateOnly(1995, 12, 15)), (t.Name, t.Year!.Value, t.RuntimeMinutes!.Value, t.ReleaseDate!.Value));
        Assert.Equal("1995-12-15", TitleFieldValues.Get(t, TitleFields.ReleaseDate));
        TitleFieldValues.Set(t, TitleFields.Year, "");
        Assert.Null(t.Year);
    }
}
