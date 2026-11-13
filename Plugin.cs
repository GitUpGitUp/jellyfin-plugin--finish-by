using Jellyfin.Plugin.FinishBy.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.FinishBy;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId = Guid.Parse("4bcae5ca-6589-47c6-9f8c-57f0cf5e2f41");

    public Plugin(IApplicationPaths paths, IXmlSerializer serializer)
        : base(paths, serializer) => Instance = this;

    public static Plugin? Instance { get; private set; }

    public override string Name => "Finish By";

    public override Guid Id => PluginId;

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = "Jellyfin.Plugin.FinishBy.Configuration.configPage.html"
        }
    ];
}
