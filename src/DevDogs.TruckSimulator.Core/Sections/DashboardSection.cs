namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Dashboard display preferences. Game-independent: updated even when no game is running.
/// </summary>
/// <param name="settings">The user settings the section reads and toggles.</param>
public sealed class DashboardSection(PluginSettings settings) : ISection
{
    /// <summary>Property: whether dashboards should show metric units.</summary>
    public const string DisplayUnitMetric = "Dashboard.DisplayUnitMetric";

    /// <summary>Action: toggles between metric and imperial units.</summary>
    public const string SwitchDisplayUnit = "SwitchDisplayUnit";

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(DisplayUnitMetric, settings.DashUnitMetric);
        host.AddAction(SwitchDisplayUnit, output =>
        {
            settings.DashUnitMetric = !settings.DashUnitMetric;
            Update(output);
        });
    }

    /// <summary>
    /// Publishes the current unit preference.
    /// </summary>
    /// <param name="output">Receives the property value.</param>
    public void Update(ISectionOutput output) => output.SetProperty(DisplayUnitMetric, settings.DashUnitMetric);
}
