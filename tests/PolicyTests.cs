using Jellyfin.Data.Enums;
using Jellyfin.Plugin.FinishBy;
using Jellyfin.Plugin.FinishBy.Configuration;
using MediaBrowser.Model.Dto;
using Xunit;

namespace FinishBy.Tests;

public class PolicyTests
{
    private static readonly DateTimeOffset MondayAtEight = new(2026, 11, 16, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DisabledDayDoesNotFilter()
    {
        Assert.Null(FinishByPolicy.GetTimeRemaining(new PluginConfiguration(), MondayAtEight));
    }

    [Fact]
    public void ItemEndingAtCutoffIsVisible()
    {
        var remaining = FinishByPolicy.GetTimeRemaining(WeekdayConfig(), MondayAtEight);
        Assert.Equal(TimeSpan.FromHours(1), remaining);
        Assert.True(FinishByPolicy.ShouldShow(Movie(TimeSpan.FromHours(1)), remaining!.Value));
        Assert.False(FinishByPolicy.ShouldShow(Movie(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1))), remaining.Value));
    }

    [Fact]
    public void ResumedItemUsesTimeLeft()
    {
        var item = Movie(TimeSpan.FromHours(2));
        item.UserData = new UserItemDataDto { Key = "movie", PlaybackPositionTicks = TimeSpan.FromMinutes(75).Ticks };
        Assert.True(FinishByPolicy.ShouldShow(item, TimeSpan.FromHours(1)));
        item.UserData.Played = true;
        Assert.False(FinishByPolicy.ShouldShow(item, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void AfterCutoffNoPlayableItemsAreVisible()
    {
        var remaining = FinishByPolicy.GetTimeRemaining(WeekdayConfig(), MondayAtEight.AddHours(2));
        Assert.Equal(TimeSpan.Zero, remaining);
        Assert.False(FinishByPolicy.ShouldShow(Movie(TimeSpan.FromMinutes(1)), remaining!.Value));
    }

    [Fact]
    public void ConfiguredTimeZoneControlsDayAndCutoff()
    {
        var config = new PluginConfiguration { TimeZoneId = "Asia/Bangkok" };
        config.Days[(int)DayOfWeek.Sunday].Enabled = true;
        config.Days[(int)DayOfWeek.Sunday].CutoffMinutes = 21 * 60;

        // Sunday 20:30 in Bangkok is Sunday 13:30 UTC.
        var now = new DateTimeOffset(2026, 11, 15, 13, 30, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMinutes(30), FinishByPolicy.GetTimeRemaining(config, now));
    }

    [Fact]
    public void PreviousDayRuleContinuesAfterMidnight()
    {
        var config = new PluginConfiguration { TimeZoneId = "UTC" };
        config.Days[(int)DayOfWeek.Friday] = new DayCutoff { Enabled = true, CutoffMinutes = 2 * 60 };
        Assert.Equal(TimeSpan.FromHours(3), FinishByPolicy.GetTimeRemaining(
            config, new DateTimeOffset(2026, 11, 13, 23, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TimeSpan.FromMinutes(30), FinishByPolicy.GetTimeRemaining(
            config, new DateTimeOffset(2026, 11, 14, 1, 30, 0, TimeSpan.Zero)));
        Assert.Null(FinishByPolicy.GetTimeRemaining(
            config, new DateTimeOffset(2026, 11, 14, 2, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void AfternoonCutoffDoesNotMoveToTomorrow()
    {
        var config = new PluginConfiguration { TimeZoneId = "UTC" };
        config.Days[(int)DayOfWeek.Friday] = new DayCutoff { Enabled = true, CutoffMinutes = 16 * 60 };
        Assert.Equal(TimeSpan.Zero, FinishByPolicy.GetTimeRemaining(
            config, new DateTimeOffset(2026, 11, 13, 16, 1, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void OverrideTemporarilyDisablesFilter()
    {
        var config = WeekdayConfig();
        config.OverrideUntilUnixSeconds = MondayAtEight.AddHours(1).ToUnixTimeSeconds();
        Assert.Null(FinishByPolicy.GetTimeRemaining(config, MondayAtEight));
        Assert.Equal(TimeSpan.Zero, FinishByPolicy.GetTimeRemaining(config, MondayAtEight.AddHours(2)));
    }

    [Fact]
    public void UnknownRuntimeIsHiddenButFoldersStayVisible()
    {
        Assert.False(FinishByPolicy.ShouldShow(new BaseItemDto { Type = BaseItemKind.Movie }, TimeSpan.FromHours(1)));
        Assert.True(FinishByPolicy.ShouldShow(new BaseItemDto { Type = BaseItemKind.Series }, TimeSpan.Zero));
    }

    private static PluginConfiguration WeekdayConfig()
    {
        var config = new PluginConfiguration();
        config.Days[(int)DayOfWeek.Monday].Enabled = true;
        config.Days[(int)DayOfWeek.Monday].CutoffMinutes = 21 * 60;
        return config;
    }

    private static BaseItemDto Movie(TimeSpan duration) => new()
    {
        Type = BaseItemKind.Movie,
        RunTimeTicks = duration.Ticks
    };
}
