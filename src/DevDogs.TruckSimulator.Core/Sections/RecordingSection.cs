using System;
using System.IO;
using DevDogs.TruckSimulator.Core.Recording;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Where new recordings are written.
/// </summary>
public interface IRecordingTarget
{
    /// <summary>
    /// Creates a new, empty recording.
    /// </summary>
    /// <param name="startedAt">The UTC time recording starts, for naming.</param>
    /// <param name="location">Where the recording is, for showing to the user.</param>
    /// <returns>A writable stream for the recording.</returns>
    Stream Create(
        DateTime startedAt,
        out string location);
}

/// <summary>
/// Opt-in recording of the telemetry the sections receive, for replaying real drives in tests.
/// Starting a recording only arms it: the file is created on the first update with game data, so
/// starting and stopping without the game running leaves no empty file.
/// </summary>
/// <param name="recorder">The background recorder.</param>
/// <param name="target">Creates recording files.</param>
/// <param name="pluginVersion">The plugin version written into each recording.</param>
public sealed class RecordingSection(
    TelemetryRecorder recorder,
    IRecordingTarget target,
    string pluginVersion) : ITelemetrySection
{
    /// <summary>Property: a recording is in progress or waiting for game data.</summary>
    public const string Recording = "Recorder.Recording";

    /// <summary>Action: starts or stops recording.</summary>
    public const string ToggleTelemetryRecording = "ToggleTelemetryRecording";

    private readonly object _gate = new();
    private volatile bool _armed;

    /// <summary>Gets a value indicating whether a recording is in progress or waiting for game data.</summary>
    public bool IsRecording => _armed || recorder.IsRecording;

    /// <summary>Gets a value indicating whether recording was started but no game data has arrived yet.</summary>
    public bool IsWaitingForData => _armed;

    /// <summary>Gets where the current or last recording is; empty before the first file is created.</summary>
    public string Location { get; private set; } = "";

    /// <summary>Gets the error that prevented the last recording file from being created, if any.</summary>
    public Exception? StartError { get; private set; }

    /// <summary>Gets the recorder, for reporting progress.</summary>
    public TelemetryRecorder Recorder => recorder;

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(Recording, false);
        host.AddAction(ToggleTelemetryRecording, output =>
        {
            Toggle();
            output.SetProperty(Recording, IsRecording);
        });
    }

    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        if (_armed)
        {
            StartFile(now);
        }

        recorder.Record(now, telemetry);
        output.SetProperty(Recording, IsRecording);
    }

    /// <summary>
    /// Arms a new recording, or stops the current one (or cancels one still waiting for data).
    /// </summary>
    public void Toggle()
    {
        lock (_gate)
        {
            if (_armed)
            {
                _armed = false;
            }
            else if (recorder.IsRecording)
            {
                recorder.Stop();
            }
            else
            {
                StartError = null;
                _armed = true;
            }
        }
    }

    /// <summary>
    /// Creates the recording file for an armed recording and starts writing to it.
    /// </summary>
    /// <param name="now">The time of the first update, used to name the file.</param>
    private void StartFile(DateTime now)
    {
        lock (_gate)
        {
            if (!_armed)
            {
                return;
            }

            _armed = false;
            try
            {
                var stream = target.Create(now, out var location);
                Location = location;
                recorder.Start(stream, pluginVersion, now);
            }
            catch (Exception ex)
            {
                // Creating the file can fail (permissions, disk full); report it on the page
                // instead of failing every update.
                StartError = ex;
            }
        }
    }
}
