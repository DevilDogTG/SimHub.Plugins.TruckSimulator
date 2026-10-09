using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using DevDogs.TruckSimulator.Core.Telemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DevDogs.TruckSimulator.Core.Recording;

/// <summary>
/// One recorded update: the time it ran and the snapshot the sections received.
/// </summary>
public sealed record RecordedTick
{
    /// <summary>Gets the UTC time of the update.</summary>
    public DateTime At { get; init; }

    /// <summary>Gets the telemetry snapshot.</summary>
    public TruckTelemetry Telemetry { get; init; } = new();
}

/// <summary>
/// The first line of a recording, identifying the format and where it came from.
/// </summary>
public sealed record RecordingHeader
{
    /// <summary>Gets the format name; always <see cref="TelemetryRecordingFormat.Name"/>.</summary>
    public string Format { get; init; } = "";

    /// <summary>Gets the format version.</summary>
    public int Version { get; init; }

    /// <summary>Gets the plugin version that wrote the recording.</summary>
    public string PluginVersion { get; init; } = "";

    /// <summary>Gets the UTC time recording started.</summary>
    public DateTime StartedAt { get; init; }
}

/// <summary>
/// Gzip-compressed JSON Lines: a <see cref="RecordingHeader"/> line, then one
/// <see cref="RecordedTick"/> per line. Fields added to the snapshot later read as their defaults
/// from older recordings.
/// </summary>
public static class TelemetryRecordingFormat
{
    /// <summary>The format name written in the header.</summary>
    public const string Name = "ddtruck-telemetry";

    /// <summary>The current format version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The file extension for recordings.</summary>
    public const string FileExtension = ".jsonl.gz";

    /// <summary>Gets the serializer settings shared by writer and reader.</summary>
    internal static JsonSerializerSettings Settings { get; } = new()
    {
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        Formatting = Formatting.None,
        Converters = [new StringEnumConverter()],
    };

    /// <summary>
    /// Reads a recording.
    /// </summary>
    /// <param name="source">The compressed recording; disposed when reading completes.</param>
    /// <returns>The recorded updates, in order.</returns>
    /// <exception cref="InvalidDataException">The stream is not a recording this version can read.</exception>
    public static IEnumerable<RecordedTick> Read(Stream source)
    {
        using var reader = new StreamReader(new GZipStream(source, CompressionMode.Decompress), Encoding.UTF8);

        var header = JsonConvert.DeserializeObject<RecordingHeader>(reader.ReadLine() ?? "", Settings);
        if (header is null || header.Format != Name || header.Version > CurrentVersion)
        {
            throw new InvalidDataException($"Not a {Name} recording of version {CurrentVersion} or older.");
        }

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            yield return JsonConvert.DeserializeObject<RecordedTick>(line, Settings)
                ?? throw new InvalidDataException("Empty recorded tick.");
        }
    }
}

/// <summary>
/// Writes a recording. Not thread-safe; <see cref="TelemetryRecorder"/> serialises access.
/// </summary>
public sealed class TelemetryRecordingWriter : IDisposable
{
    private readonly StreamWriter _writer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryRecordingWriter"/> class and writes the header.
    /// </summary>
    /// <param name="destination">The stream to write to; disposed with the writer.</param>
    /// <param name="pluginVersion">The plugin version recorded in the header.</param>
    /// <param name="startedAt">The UTC time recording started.</param>
    public TelemetryRecordingWriter(
        Stream destination,
        string pluginVersion,
        DateTime startedAt)
    {
        _writer = new StreamWriter(new GZipStream(destination, CompressionLevel.Fastest), new UTF8Encoding(false));

        WriteLine(new RecordingHeader
        {
            Format = TelemetryRecordingFormat.Name,
            Version = TelemetryRecordingFormat.CurrentVersion,
            PluginVersion = pluginVersion,
            StartedAt = startedAt,
        });
    }

    /// <summary>
    /// Appends one update.
    /// </summary>
    /// <param name="tick">The update to write.</param>
    public void Write(RecordedTick tick) => WriteLine(tick);

    /// <summary>
    /// Flushes and closes the recording.
    /// </summary>
    public void Dispose() => _writer.Dispose();

    /// <summary>
    /// Writes a value as one JSON line.
    /// </summary>
    /// <param name="value">The value to write.</param>
    private void WriteLine(object value) =>
        _writer.WriteLine(JsonConvert.SerializeObject(value, TelemetryRecordingFormat.Settings));
}
