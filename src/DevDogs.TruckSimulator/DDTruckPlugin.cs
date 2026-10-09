using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using DevDogs.TruckSimulator.Core;
using DevDogs.TruckSimulator.Core.Localisation;
using DevDogs.TruckSimulator.Core.Recording;
using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Telemetry;
using DevDogs.TruckSimulator.UI;
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
public class DDTruckPlugin : IPlugin, IDataPlugin, IWPFSettings
{
    /// <summary>The key SimHub stores this plugin's settings under.</summary>
    private const string SettingsKey = "DDTruckPluginSettings";

    private readonly HashSet<string> _failingSections = [];

    private SimHubSectionBridge _bridge = null!; // assigned in Init, which SimHub calls before any other member
    private DashboardSection _dashboard = null!; // assigned in Init
    private RecordingSection _recording = null!; // assigned in Init
    private string _recordingsFolder = "";
    private ITelemetrySection[] _sections = [];

    /// <summary>
    /// Gets or sets the SimHub plugin manager; assigned by SimHub before <see cref="Init"/> is called.
    /// </summary>
    public PluginManager PluginManager { get; set; } = null!; // set by SimHub before Init

    /// <summary>
    /// Gets the user settings; loaded in <see cref="Init"/>.
    /// </summary>
    public PluginSettings Settings { get; private set; } = new();

    /// <summary>
    /// Loads settings and declares every section's properties, events and actions.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void Init(PluginManager pluginManager)
    {
        Settings = this.ReadCommonSettings(SettingsKey, () => new PluginSettings());
        _bridge = new SimHubSectionBridge(pluginManager, GetType());

        _dashboard = new DashboardSection(Settings);

        _recordingsFolder = Path.Combine(pluginManager.GetCommonStoragePath(), "DevDogs.TruckSimulator", "Recordings");
        _recording = new RecordingSection(new TelemetryRecorder(), new FileRecordingTarget(_recordingsFolder), InformationalVersion);

        _sections =
        [
            new DamageSection(Settings),
            new DrivetrainSection(),
            new EngineSection(),
            new JobSection(Settings),
            new JobStatusSection(),
            new LightsSection(),
            new LocalisationSection(EmptyCityNameSource.Instance),
            new NavigationSection(),
            _recording,
        ];

        _dashboard.Register(_bridge);
        foreach (var section in _sections)
        {
            section.Register(_bridge);
        }

        SimHub.Logging.Current.Info($"DevDogs.TruckSimulator {InformationalVersion} loaded; recordings go to {_recordingsFolder}");
    }

    /// <summary>
    /// Updates every section from the current telemetry. Telemetry sections only run while ETS2 or
    /// ATS is running and reporting data.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    /// <param name="data">The current game data.</param>
    public void DataUpdate(
        PluginManager pluginManager,
        ref GameData data)
    {
        _dashboard.Update(_bridge);

        if (!TelemetryReader.TryRead(ref data, out var telemetry) || telemetry is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var section in _sections)
        {
            try
            {
                section.Update(telemetry, now, _bridge);
            }
            catch (Exception ex)
            {
                // One faulty section must not stop the others; log once per section to avoid
                // flooding the log at the telemetry rate.
                if (_failingSections.Add(section.GetType().Name))
                {
                    SimHub.Logging.Current.Error($"DevDogs.TruckSimulator: {section.GetType().Name} failed", ex);
                }
            }
        }
    }

    /// <summary>
    /// Finishes any recording and saves settings when SimHub shuts down.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    public void End(PluginManager pluginManager)
    {
        _recording.Recorder.Dispose();
        this.SaveCommonSettings(SettingsKey, Settings);
    }

    /// <summary>
    /// Creates the settings page shown in SimHub.
    /// </summary>
    /// <param name="pluginManager">The SimHub plugin manager.</param>
    /// <returns>The settings control.</returns>
    public Control GetWPFSettingsControl(PluginManager pluginManager) => new SettingsControl(Settings, _recording, _recordingsFolder, InformationalVersion);

    /// <summary>
    /// Gets the version including any pre-release suffix, for example <c>0.1.0-dev.1</c>.
    /// </summary>
    private static string InformationalVersion =>
        typeof(DDTruckPlugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "unknown";
}
