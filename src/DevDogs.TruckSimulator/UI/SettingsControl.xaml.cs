using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using DevDogs.TruckSimulator.Core.Diagnostics;
using DevDogs.TruckSimulator.Telemetry;

namespace DevDogs.TruckSimulator.UI;

/// <summary>
/// The plugin's page in SimHub: settings, the live telemetry view and recording controls.
/// Settings controls bind directly to the settings object the sections read, so changes apply
/// immediately.
/// </summary>
public partial class SettingsControl : UserControl
{
    private readonly SettingsPageModel _model;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly ObservableCollection<LiveRow> _inputs = [];
    private readonly ObservableCollection<LiveRow> _outputs = [];
    private readonly Dictionary<string, LiveRow> _inputsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LiveRow> _outputsByName = new(StringComparer.Ordinal);
    private int _recordingStatusCountdown;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsControl"/> class.
    /// </summary>
    /// <param name="model">What the page shows and controls.</param>
    internal SettingsControl(SettingsPageModel model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        VersionText.Text = $"Version {model.Version}";

        InputsGrid.ItemsSource = _inputs;
        OutputsGrid.ItemsSource = _outputs;
        CollectionViewSource.GetDefaultView(_inputs).Filter = MatchesFilter;
        CollectionViewSource.GetDefaultView(_outputs).Filter = MatchesFilter;

        _timer.Tick += (_, _) => OnTimer();
    }

    /// <summary>
    /// Starts the refresh timer while the page is shown.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The event data.</param>
    private void UserControl_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        RefreshRecordingStatus();
        _timer.Start();
    }

    /// <summary>
    /// Stops refreshing and stops live capture when the page is left; the live view is not persisted.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The event data.</param>
    private void UserControl_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _timer.Stop();
        LiveSwitch.IsChecked = false;
    }

    /// <summary>
    /// Refreshes the live view (5 times a second) while its tab is open and switched on, and the
    /// recording status twice a second.
    /// </summary>
    private void OnTimer()
    {
        if (LiveTab.IsSelected && LiveSwitch.IsChecked == true)
        {
            RefreshLive();
        }

        if (--_recordingStatusCountdown <= 0)
        {
            _recordingStatusCountdown = 2;
            RefreshRecordingStatus();
        }
    }

    /// <summary>
    /// Turns live capture on or off.
    /// </summary>
    /// <param name="sender">The switch.</param>
    /// <param name="e">The event data.</param>
    private void LiveSwitch_Changed(
        object sender,
        RoutedEventArgs e)
    {
        var on = LiveSwitch.IsChecked == true;
        _model.Monitor.Enabled = on;
        LiveStatusText.Text = on ? "Waiting for the next update..." : "Live values are off.";

        if (on)
        {
            _inputs.Clear();
            _outputs.Clear();
            _inputsByName.Clear();
            _outputsByName.Clear();
            EventsList.Items.Clear();
        }
    }

    /// <summary>
    /// Re-applies the name filter.
    /// </summary>
    /// <param name="sender">The filter box.</param>
    /// <param name="e">The event data.</param>
    private void FilterBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(_inputs).Refresh();
        CollectionViewSource.GetDefaultView(_outputs).Refresh();
    }

    /// <summary>
    /// Whether a row matches the filter text, by name or source.
    /// </summary>
    /// <param name="item">The row.</param>
    /// <returns><see langword="true"/> when the row should be shown.</returns>
    private bool MatchesFilter(object item)
    {
        var filter = FilterBox.Text.Trim();
        return filter.Length == 0
            || item is LiveRow row
                && (row.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || row.Source.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// Shows the latest captured values.
    /// </summary>
    private void RefreshLive()
    {
        var view = _model.Monitor.Capture();

        LiveStatusText.Text = DescribeStatus(view);

        if (view.Telemetry is not null)
        {
            foreach (var field in SnapshotFields.Flatten(view.Telemetry))
            {
                Row(_inputs, _inputsByName, field.Key, TelemetryReader.Sources.TryGetValue(field.Key, out var source) ? source : "")
                    .Update(field.Value);
            }
        }

        foreach (var output in view.Outputs)
        {
            Row(_outputs, _outputsByName, _model.PropertyPrefix + output.Key, "").Update(output.Value);
        }

        var events = view.Events
            .Select(e => e.At.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + _model.PropertyPrefix + e.Name)
            .ToList();
        if (!events.SequenceEqual(EventsList.Items.Cast<string>()))
        {
            EventsList.Items.Clear();
            events.ForEach(e => EventsList.Items.Add(e));
        }
    }

    /// <summary>
    /// Describes the game state, and any failing sections, in one line.
    /// </summary>
    /// <param name="view">The captured values.</param>
    /// <returns>The status text.</returns>
    private static string DescribeStatus(LiveTelemetryView view)
    {
        var status = view.Status switch
        {
            null => "Waiting for the next update...",
            { GameRunning: false } => "No game running.",
            { HasTelemetry: false } => $"{view.Status.Game}: running, but no ETS2/ATS telemetry this update.",
            { Paused: true } => $"{view.Status.Game}: paused.",
            _ => $"{view.Status.Game}: running.",
        };

        return view.FailingSections.Count == 0
            ? status
            : status + " Failing sections (see SimHub log): " + string.Join(", ", view.FailingSections);
    }

    /// <summary>
    /// Finds a row by name, adding it when new.
    /// </summary>
    /// <param name="rows">The grid's rows.</param>
    /// <param name="byName">The rows by name.</param>
    /// <param name="name">The row name.</param>
    /// <param name="source">The row's source, used when the row is created.</param>
    /// <returns>The row.</returns>
    private static LiveRow Row(
        ObservableCollection<LiveRow> rows,
        Dictionary<string, LiveRow> byName,
        string name,
        string source)
    {
        if (!byName.TryGetValue(name, out var row))
        {
            row = new LiveRow(name, source);
            byName[name] = row;
            rows.Add(row);
        }

        return row;
    }

    /// <summary>
    /// Copies the selected input's SimHub source to the clipboard.
    /// </summary>
    /// <param name="sender">The menu item.</param>
    /// <param name="e">The event data.</param>
    private void CopyInputSource_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (InputsGrid.SelectedItem is LiveRow { Source.Length: > 0 } row)
        {
            Clipboard.SetText(row.Source);
        }
    }

    /// <summary>
    /// Copies the selected output's full property name to the clipboard.
    /// </summary>
    /// <param name="sender">The menu item.</param>
    /// <param name="e">The event data.</param>
    private void CopyOutputName_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (OutputsGrid.SelectedItem is LiveRow row)
        {
            Clipboard.SetText(row.Name);
        }
    }

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
            _model.Recording.Toggle(DateTime.UtcNow);
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
        Directory.CreateDirectory(_model.RecordingsFolder);
        Process.Start(new ProcessStartInfo(_model.RecordingsFolder) { UseShellExecute = true });
    }

    /// <summary>
    /// Shows whether a recording is running, where it goes and how much it has written.
    /// </summary>
    private void RefreshRecordingStatus()
    {
        var recording = _model.Recording;
        var recorder = recording.Recorder;
        RecordButton.Content = recorder.IsRecording ? "Stop recording" : "Start recording";

        RecordingStatus.Text = recorder switch
        {
            { IsRecording: true } => $"Recording {recorder.RecordedTicks:N0} updates to {recording.Location}"
                + (recorder.DroppedTicks > 0 ? $" ({recorder.DroppedTicks:N0} dropped)" : ""),
            { LastError: { } error } => $"Recording stopped: {error.Message}",
            _ when recording.Location.Length > 0 => $"Last recording: {recorder.RecordedTicks:N0} updates in {recording.Location}",
            _ => "Not recording. Recordings only capture while ETS2 or ATS is running.",
        };
    }
}
