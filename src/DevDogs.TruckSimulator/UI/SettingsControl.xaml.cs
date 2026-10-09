using System.Windows.Controls;
using DevDogs.TruckSimulator.Core;

namespace DevDogs.TruckSimulator.UI;

/// <summary>
/// The plugin's settings page in SimHub. Controls bind directly to <see cref="PluginSettings"/>,
/// which the sections read on every update, so changes apply immediately.
/// </summary>
public partial class SettingsControl : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsControl"/> class.
    /// </summary>
    /// <param name="settings">The settings to edit.</param>
    /// <param name="version">The plugin version to show.</param>
    public SettingsControl(
        PluginSettings settings,
        string version)
    {
        InitializeComponent();
        DataContext = settings;
        VersionText.Text = $"Version {version}";
    }
}
