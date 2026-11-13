using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FinishBy.Configuration;

public sealed class DayCutoff
{
    public bool Enabled { get; set; }

    // Minutes after midnight in the server's local time zone.
    public int CutoffMinutes { get; set; } = 21 * 60;

    // The cutoff time falls on the following calendar day.
    public bool NextDay { get; set; }
}

public sealed class PluginConfiguration : BasePluginConfiguration
{
    // Empty keeps the server's local time zone until a zone is saved in settings.
    public string TimeZoneId { get; set; } = string.Empty;

    public long OverrideUntilUnixSeconds { get; set; }

    // The indices match DayOfWeek (Sunday = 0).
    public DayCutoff[] Days { get; set; } = Enumerable.Range(0, 7)
        .Select(_ => new DayCutoff())
        .ToArray();
}
