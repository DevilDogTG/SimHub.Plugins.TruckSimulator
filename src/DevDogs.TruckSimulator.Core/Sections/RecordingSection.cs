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
/// </summary>
/// <param name="recorder">The background recorder.</param>
/// <param name="target">Creates recording files.</param>
/// <param name="pluginVersion">The plugin version written into each recording.</param>
public sealed class RecordingSection(
    TelemetryRecorder recorder,
    IRecordingTarget target,
    string pluginVersion) : ITelemetrySection
{
    /// <summary>Property: a recording is in progress.</summary>
    public const string Recording = "Recorder.Recording";

    /// <summary>Action: starts or stops recording.</summary>
    public const string ToggleTelemetryRecording = "ToggleTelemetryRecording";

    /// <summary>Gets a value indicating whether a recording is in progress.</summary>
    public bool IsRecording => recorder.IsRecording;

    /// <summary>Gets where the current or last recording is; empty before the first recording.</summary>
    public string Location { get; private set; } = "";

    /// <summary>Gets the recorder, for reporting progress.</summary>
    public TelemetryRecorder Recorder => recorder;

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(Recording, false);
        host.AddAction(ToggleTelemetryRecording, output =>
        {
            Toggle(DateTime.UtcNow);
            output.SetProperty(Recording, recorder.IsRecording);
        });
    }

    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        recorder.Record(now, telemetry);
        output.SetProperty(Recording, recorder.IsRecording);
    }

    /// <summary>
    /// Starts a new recording, or stops the current one.
    /// </summary>
    /// <param name="now">The current UTC time.</param>
    public void Toggle(DateTime now)
    {
        if (recorder.IsRecording)
        {
            recorder.Stop();
            return;
        }

        var stream = target.Create(now, out var location);
        Location = location;
        recorder.Start(stream, pluginVersion, now);
    }
}
