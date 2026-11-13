using Jellyfin.Data.Enums;
using Jellyfin.Plugin.FinishBy.Configuration;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.FinishBy;

public static class FinishByPolicy
{
    public static TimeSpan? GetTimeRemaining(PluginConfiguration configuration, DateTimeOffset now)
    {
        if (configuration.OverrideUntilUnixSeconds > now.ToUnixTimeSeconds())
        {
            return null;
        }

        var zone = !string.IsNullOrWhiteSpace(configuration.TimeZoneId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(configuration.TimeZoneId, out var configuredZone)
            ? configuredZone
            : TimeZoneInfo.Local;
        var local = TimeZoneInfo.ConvertTime(now, zone);
        if (configuration.Days is null || configuration.Days.Length < 7)
        {
            return null;
        }

        var previousDay = configuration.Days[((int)local.DayOfWeek + 6) % 7];
        if (previousDay is { Enabled: true, NextDay: true, CutoffMinutes: >= 0 and <= 1439 })
        {
            var previousCutoff = MakeCutoff(local.Date.AddDays(-1), previousDay.CutoffMinutes, true, zone);
            if (now < previousCutoff)
            {
                return previousCutoff - now;
            }
        }

        var day = configuration.Days[(int)local.DayOfWeek];
        if (day is null || !day.Enabled || day.CutoffMinutes is < 0 or > 1439)
        {
            return null;
        }

        var cutoff = MakeCutoff(local.Date, day.CutoffMinutes, day.NextDay, zone);
        var remaining = cutoff - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static DateTimeOffset MakeCutoff(DateTime date, int minutes, bool nextDay, TimeZoneInfo zone)
    {
        var localCutoff = DateTime.SpecifyKind(date.AddDays(nextDay ? 1 : 0).AddMinutes(minutes), DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(localCutoff))
        {
            localCutoff = localCutoff.AddMinutes(1);
        }

        // Choose the earlier occurrence when the clock repeats an hour.
        var offset = zone.IsAmbiguousTime(localCutoff)
            ? zone.GetAmbiguousTimeOffsets(localCutoff).Max()
            : zone.GetUtcOffset(localCutoff);
        return new DateTimeOffset(localCutoff, offset);
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
