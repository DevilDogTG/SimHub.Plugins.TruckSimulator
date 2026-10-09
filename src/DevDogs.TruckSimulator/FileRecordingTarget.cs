using System;
using System.Globalization;
using System.IO;
using DevDogs.TruckSimulator.Core.Recording;
using DevDogs.TruckSimulator.Core.Sections;

namespace DevDogs.TruckSimulator;

/// <summary>
/// Writes each recording to a new file named after its UTC start time.
/// </summary>
/// <param name="directory">The folder recordings are written to; created when needed.</param>
internal sealed class FileRecordingTarget(string directory) : IRecordingTarget
{
    /// <inheritdoc />
    public Stream Create(
        DateTime startedAt,
        out string location)
    {
        System.IO.Directory.CreateDirectory(directory);

        var name = "telemetry-" + startedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "Z" + TelemetryRecordingFormat.FileExtension;
        location = Path.Combine(directory, name);

        return new FileStream(location, FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize: 64 * 1024);
    }
}
