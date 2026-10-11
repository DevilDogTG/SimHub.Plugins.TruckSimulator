using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DevDogs.TruckSimulator.DataTools.Cameras;

/// <summary>
/// Writes <c>camera_db.csv</c> and <c>camera_db.meta.json</c>, the files the plugin embeds. The CSV
/// matches the DriveDogs Radar extraction byte for byte, plus a trailing <c>facing</c> column.
/// </summary>
internal static class CameraDatabaseFile
{
    /// <summary>The CSV columns, in order.</summary>
    internal static readonly string[] Columns = ["uid", "x", "y", "z", "unit", "model", "sector", "facing"];

    /// <summary>
    /// Writes both files into a folder.
    /// </summary>
    /// <param name="directory">The output folder; created when needed.</param>
    /// <param name="game">The game id, <c>ets2</c> or <c>ats</c>.</param>
    /// <param name="gameVersion">The game version the map was extracted from.</param>
    /// <param name="cameras">The cameras.</param>
    public static void Write(
        string directory,
        string game,
        string gameVersion,
        IReadOnlyList<MapCamera> cameras)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "camera_db.csv"), ToCsv(cameras), new UTF8Encoding(false));

        var meta = new Dictionary<string, object>
        {
            ["game"] = game,
            ["game_version"] = gameVersion,
            ["cameras"] = cameras.Count,
            ["source"] = "DevDogs.TruckSimulator tools/DevDogs.TruckSimulator.DataTools (cameras)",
            ["columns"] = Columns,
            ["coordinates"] = "world space, metres - same as SCS telemetry truck.world.placement",
            ["facing"] = "compass degrees of the sign node's forward axis = travel direction the camera enforces "
                + "(0 = -Z north, 90 = +X east); empty when the node wasn't found",
        };
        // Relaxed escaping keeps ' and + readable; the file is metadata read by people and the plugin,
        // never embedded in HTML, so HTML-safe escaping buys nothing.
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(Path.Combine(directory, "camera_db.meta.json"), json.Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
    }

    /// <summary>
    /// Formats the cameras as CSV with LF line endings.
    /// </summary>
    /// <param name="cameras">The cameras.</param>
    /// <returns>The CSV text, header first.</returns>
    internal static string ToCsv(IReadOnlyList<MapCamera> cameras)
    {
        var csv = new StringBuilder();
        csv.Append(string.Join(",", Columns)).Append('\n');
        foreach (var c in cameras)
        {
            csv.Append(c.Uid.ToString("x16", CultureInfo.InvariantCulture)).Append(',')
                .Append(Number(c.X)).Append(',')
                .Append(Number(c.Y)).Append(',')
                .Append(Number(c.Z)).Append(',')
                .Append(c.Unit).Append(',')
                .Append(c.Model).Append(',')
                .Append(c.Sector).Append(',')
                .Append(c.Facing is { } facing ? Number(facing) : "")
                .Append('\n');
        }

        return csv.ToString();
    }

    /// <summary>
    /// Formats a number the way Python prints a float: shortest round-trip digits, always with a
    /// decimal point (<c>44.0</c>, <c>916.5</c>).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    internal static string Number(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.') || text.Contains('E') ? text : text + ".0";
    }
}
