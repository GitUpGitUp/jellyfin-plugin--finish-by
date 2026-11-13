using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FinishBy.Configuration;

public sealed class DayCutoff
{
    public bool Enabled { get; set; }

    // Minutes after midnight in the server's local time zone.
    public int CutoffMinutes { get; set; } = 21 * 60;
}

public sealed class PluginConfiguration : BasePluginConfiguration
{
    // The indices match DayOfWeek (Sunday = 0).
    public DayCutoff[] Days { get; set; } = Enumerable.Range(0, 7)
        .Select(_ => new DayCutoff())
        .ToArray();
}
