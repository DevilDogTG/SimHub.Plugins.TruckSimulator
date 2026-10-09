using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DevDogs.TruckSimulator.Core;
using DevDogs.TruckSimulator.Core.Sections;

namespace DevDogs.TruckSimulator.UI;

/// <summary>
/// The plugin's settings page in SimHub. Controls bind directly to <see cref="PluginSettings"/>,
/// which the sections read on every update, so changes apply immediately.
/// </summary>
public partial class SettingsControl : UserControl
{
    private readonly RecordingSection _recording;
    private readonly string _recordingsFolder;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsControl"/> class.
    /// </summary>
    /// <param name="settings">The settings to edit.</param>
    /// <param name="recording">The telemetry recording section to control.</param>
    /// <param name="recordingsFolder">The folder recordings are written to.</param>
    /// <param name="version">The plugin version to show.</param>
    public SettingsControl(
        PluginSettings settings,
        RecordingSection recording,
        string recordingsFolder,
        string version)
    {
        InitializeComponent();
        DataContext = settings;
        VersionText.Text = $"Version {version}";

        _recording = recording;
        _recordingsFolder = recordingsFolder;
        _statusTimer.Tick += (_, _) => RefreshRecordingStatus();
    }

    /// <summary>
    /// Starts refreshing the recording status while the page is shown.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The event data.</param>
    private void UserControl_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        RefreshRecordingStatus();
        _statusTimer.Start();
    }

    /// <summary>
    /// Stops refreshing the recording status when the page is hidden.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The event data.</param>
    private void UserControl_Unloaded(
        object sender,
        RoutedEventArgs e) => _statusTimer.Stop();

    /// <summary>
    /// Starts or stops recording.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private void RecordButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            _recording.Toggle(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            // Creating the file can fail (permissions, disk full); report it rather than crash SimHub's UI.
            SimHub.Logging.Current.Error("DevDogs.TruckSimulator: could not start recording", ex);
            MessageBox.Show(ex.Message, "Telemetry recording", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        RefreshRecordingStatus();
    }

    /// <summary>
    /// Opens the recordings folder in Explorer.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event data.</param>
    private void OpenFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Directory.CreateDirectory(_recordingsFolder);
        Process.Start(new ProcessStartInfo(_recordingsFolder) { UseShellExecute = true });
    }

    /// <summary>
    /// Shows whether a recording is running, where it goes and how much it has written.
    /// </summary>
    private void RefreshRecordingStatus()
    {
        var recorder = _recording.Recorder;
        RecordButton.Content = recorder.IsRecording ? "Stop recording" : "Start recording";

        RecordingStatus.Text = recorder switch
        {
            { IsRecording: true } => $"Recording {recorder.RecordedTicks:N0} updates to {_recording.Location}"
                + (recorder.DroppedTicks > 0 ? $" ({recorder.DroppedTicks:N0} dropped)" : ""),
            { LastError: { } error } => $"Recording stopped: {error.Message}",
            _ when _recording.Location.Length > 0 => $"Last recording: {recorder.RecordedTicks:N0} updates in {_recording.Location}",
            _ => "Not recording. Recordings only capture while ETS2 or ATS is running.",
        };
    }
}
