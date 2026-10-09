using System;
using DevDogs.TruckSimulator.Core.Sections;
using SimHub.Plugins;

namespace DevDogs.TruckSimulator;

/// <summary>
/// Connects the Core sections to SimHub. SimHub prefixes every name with the plugin type's name,
/// which gives the <c>DDTruckPlugin.</c> prefix (docs/adr/ADR-0002).
/// </summary>
/// <param name="pluginManager">The SimHub plugin manager.</param>
/// <param name="pluginType">The plugin type that owns the properties, events and actions.</param>
internal sealed class SimHubSectionBridge(
    PluginManager pluginManager,
    Type pluginType) : ISectionHost, ISectionOutput
{
    /// <inheritdoc />
    public void AddProperty<T>(
        string name,
        T defaultValue)
        where T : notnull => pluginManager.AddProperty(name, pluginType, defaultValue, "");

    /// <inheritdoc />
    public void AddEvent(string name) => pluginManager.AddEvent(name, pluginType);

    /// <inheritdoc />
    public void AddAction(
        string name,
        Action<ISectionOutput> body) =>
        pluginManager.AddAction(name, pluginType, (_, _) => body(this), (_, _) => { });

    /// <inheritdoc />
    public void SetProperty(
        string name,
        object value) => pluginManager.SetPropertyValue(name, pluginType, value);

    /// <inheritdoc />
    public void TriggerEvent(string name) => pluginManager.TriggerEvent(name, pluginType);
}
