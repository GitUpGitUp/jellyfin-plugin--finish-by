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
