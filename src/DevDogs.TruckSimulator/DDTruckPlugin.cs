using GameReaderCommon;
using SimHub.Plugins;

namespace DevDogs.TruckSimulator;

/// <summary>
/// SimHub entry point. The class name is the prefix of every property, event and action this plugin
/// registers (<c>DDTruckPlugin.*</c>) and must not change (docs/adr/ADR-0002).
/// </summary>
[PluginName("DevDogs Truck Simulator")]
[PluginDescription("Additional properties, events and actions for Euro Truck Simulator 2 and American Truck Simulator.")]
[PluginAuthor("DevDogs")]
public class DDTruckPlugin : IPlugin, IDataPlugin
{
    /// <summary>
    /// Gets or sets the SimHub plugin manager; assigned by SimHub before <see cref="Init"/> is called.
    /// </summary>
    public PluginManager PluginManager { get; set; } = null!; // set by SimHub before Init

    /// <summary>
    /// Called once by SimHub when the plugin is loaded.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void Init(PluginManager pluginManager)
    {
    }

    /// <summary>
    /// Called by SimHub on every telemetry update.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    /// <param name="data">The current game data.</param>
    public void DataUpdate(
        PluginManager pluginManager,
        ref GameData data)
    {
    }

    /// <summary>
    /// Called once by SimHub when it shuts down.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void End(PluginManager pluginManager)
    {
    }
}
