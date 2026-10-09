using System;
using System.ComponentModel;
using System.Globalization;
using DevDogs.TruckSimulator.Core;
using DevDogs.TruckSimulator.Core.Diagnostics;
using DevDogs.TruckSimulator.Core.Sections;

namespace DevDogs.TruckSimulator.UI;

/// <summary>
/// Everything the plugin page needs from the plugin.
/// </summary>
/// <param name="Settings">The user settings to edit.</param>
/// <param name="Recording">The telemetry recording section to control.</param>
/// <param name="RecordingsFolder">The folder recordings are written to.</param>
/// <param name="Monitor">The live telemetry capture.</param>
/// <param name="PropertyPrefix">The prefix SimHub puts before this plugin's names, for example <c>DDTruckPlugin.</c>.</param>
/// <param name="Version">The plugin version to show.</param>
internal sealed record SettingsPageModel(
    PluginSettings Settings,
    RecordingSection Recording,
    string RecordingsFolder,
    LiveTelemetryMonitor Monitor,
    string PropertyPrefix,
    string Version);

/// <summary>
/// One row of the live telemetry tab. Rows are created once per name and updated in place, so the
/// grids keep their scroll position and selection while values change.
/// </summary>
/// <param name="name">The field or property name.</param>
/// <param name="source">Where the value comes from; empty when not applicable.</param>
internal sealed class LiveRow(
    string name,
    string source) : INotifyPropertyChanged
{
    private string _value = "";

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the field or property name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets where the value comes from.</summary>
    public string Source { get; } = source;

    /// <summary>Gets the formatted current value.</summary>
    public string Value
    {
        get => _value;
        private set
        {
            if (_value != value)
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    /// <summary>
    /// Shows a new value.
    /// </summary>
    /// <param name="value">The raw value.</param>
    public void Update(object? value) => Value = Format(value);

    /// <summary>
    /// Formats a value for display: numbers to three decimals, culture-independent.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The display text.</returns>
    internal static string Format(object? value) => value switch
    {
        null => "",
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        TimeSpan t => t.ToString("c", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
