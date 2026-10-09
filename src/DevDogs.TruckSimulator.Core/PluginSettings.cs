namespace DevDogs.TruckSimulator.Core;

/// <summary>
/// User settings, persisted by SimHub under the key <c>DDTruckPluginSettings</c>.
/// Initialisers are the defaults for a new install.
/// </summary>
public sealed class PluginSettings
{
    /// <summary>Gets or sets the mph above (or below, when negative) the speed limit at which speeding starts.</summary>
    public int OverSpeedMargin { get; set; } = 3;

    /// <summary>Gets or sets the average wear percentage above which <c>Damage.WearWarning</c> turns on.</summary>
    public int WearWarningLevel { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether dashboards should show metric units.</summary>
    public bool DashUnitMetric { get; set; }

    /// <summary>Gets or sets the language code used for city and country names, for example <c>en_gb</c>.</summary>
    public string LocalisationLanguage { get; set; } = "en_gb";
}
