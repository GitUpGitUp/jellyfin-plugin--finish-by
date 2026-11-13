using Jellyfin.Data.Enums;
using Jellyfin.Plugin.FinishBy.Configuration;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.FinishBy;

public static class FinishByPolicy
{
    public static TimeSpan? GetTimeRemaining(PluginConfiguration configuration, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.Local;
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var index = (int)local.DayOfWeek;
        if (configuration.Days is null || index >= configuration.Days.Length)
        {
            return null;
        }

        var day = configuration.Days[index];
        if (day is null || !day.Enabled || day.CutoffMinutes is < 0 or > 1439)
        {
            return null;
        }

        var localCutoff = DateTime.SpecifyKind(local.Date.AddMinutes(day.CutoffMinutes), DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(localCutoff))
        {
            localCutoff = localCutoff.AddMinutes(1);
        }

        // Choose the earlier occurrence when the clock repeats an hour.
        var offset = zone.IsAmbiguousTime(localCutoff)
            ? zone.GetAmbiguousTimeOffsets(localCutoff).Max()
            : zone.GetUtcOffset(localCutoff);
        var cutoff = new DateTimeOffset(localCutoff, offset);
        var remaining = cutoff - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public static bool ShouldShow(BaseItemDto item, TimeSpan remaining)
    {
        if (item.Type is not (BaseItemKind.Movie or BaseItemKind.Episode or BaseItemKind.Video
            or BaseItemKind.MusicVideo or BaseItemKind.Trailer or BaseItemKind.Recording
            or BaseItemKind.Audio or BaseItemKind.AudioBook))
        {
            return true;
        }

        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        if (item.RunTimeTicks is not > 0)
        {
            return false;
        }

        var savedPosition = item.UserData?.Played == true ? 0 : item.UserData?.PlaybackPositionTicks ?? 0;
        var position = Math.Clamp(savedPosition, 0, item.RunTimeTicks.Value);
        return item.RunTimeTicks.Value - position <= remaining.Ticks;
    }
}
