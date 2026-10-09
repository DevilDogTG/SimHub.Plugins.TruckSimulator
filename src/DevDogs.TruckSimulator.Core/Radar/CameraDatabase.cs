using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DevDogs.TruckSimulator.Core.Telemetry;
using Newtonsoft.Json;

namespace DevDogs.TruckSimulator.Core.Radar;

/// <summary>
/// A fining speed camera placed on the map.
/// </summary>
/// <param name="Uid">The map item id.</param>
/// <param name="X">World X, in metres (same space as the truck's telemetry position).</param>
/// <param name="Y">World Y (height), in metres.</param>
/// <param name="Z">World Z, in metres.</param>
/// <param name="Model">The camera model, for example <c>speed_camera_ch</c>.</param>
public sealed record SpeedCamera(
    string Uid,
    double X,
    double Y,
    double Z,
    string Model);

/// <summary>
/// The speed cameras of one game, extracted from that game's map for one game version.
/// </summary>
/// <param name="Game">The game the cameras belong to.</param>
/// <param name="GameVersion">The game version the map was extracted from, for example <c>1.61.1.1</c>.</param>
/// <param name="Cameras">The cameras.</param>
public sealed record CameraDatabase(
    TruckGame Game,
    string GameVersion,
    IReadOnlyList<SpeedCamera> Cameras)
{
    private const string ResourcePrefix = "DevDogs.TruckSimulator.Radar/";

    /// <summary>
    /// Loads the database built into the plugin for a game.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <returns>The database, or <see langword="null"/> when the plugin has none for that game.</returns>
    public static CameraDatabase? LoadEmbedded(TruckGame game)
    {
        var folder = game switch
        {
            TruckGame.Ets2 => "ets2",
            TruckGame.Ats => "ats",
            _ => null,
        };
        if (folder is null)
        {
            return null;
        }

        var assembly = typeof(CameraDatabase).Assembly;
        using var csv = OpenResource(assembly, folder + "/camera_db.csv");
        using var meta = OpenResource(assembly, folder + "/camera_db.meta.json");
        if (csv is null || meta is null)
        {
            return null;
        }

        return Parse(game, new StreamReader(meta).ReadToEnd(), new StreamReader(csv));
    }

    /// <summary>
    /// Reads a database: the metadata JSON and the <c>uid,x,y,z,unit,model,sector</c> CSV written by
    /// the DriveDogs Radar extraction tools. Malformed rows are skipped.
    /// </summary>
    /// <param name="game">The game the database belongs to.</param>
    /// <param name="metaJson">The <c>camera_db.meta.json</c> content.</param>
    /// <param name="csv">The <c>camera_db.csv</c> content, header first.</param>
    /// <returns>The database.</returns>
    /// <exception cref="InvalidDataException">The metadata names a different game.</exception>
    public static CameraDatabase Parse(
        TruckGame game,
        string metaJson,
        TextReader csv)
    {
        var meta = JsonConvert.DeserializeObject<Meta>(metaJson) ?? new Meta();
        if (!string.Equals(meta.Game, game.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Camera database is for '{meta.Game}', not {game}.");
        }

        var cameras = new List<SpeedCamera>();
        csv.ReadLine();
        while (csv.ReadLine() is { } line)
        {
            var cells = line.Split(',');
            if (cells.Length >= 6
                && TryParse(cells[1], out var x)
                && TryParse(cells[2], out var y)
                && TryParse(cells[3], out var z))
            {
                cameras.Add(new SpeedCamera(cells[0], x, y, z, cells[5]));
            }
        }

        return new CameraDatabase(game, meta.GameVersion ?? "", cameras);
    }

    /// <summary>
    /// Parses a coordinate written with an invariant decimal point.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the text is a number.</returns>
    private static bool TryParse(
        string text,
        out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Opens an embedded resource by its path under the radar data folder. MSBuild writes the folder
    /// separator of the logical name as the OS separator, so both separators are accepted.
    /// </summary>
    /// <param name="assembly">The assembly holding the resources.</param>
    /// <param name="path">The path, for example <c>ets2/camera_db.csv</c>.</param>
    /// <returns>The stream, or <see langword="null"/> when there is no such resource.</returns>
    private static Stream? OpenResource(
        Assembly assembly,
        string path)
    {
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.Replace('\\', '/').Equals(ResourcePrefix + path, StringComparison.OrdinalIgnoreCase));

        return name is null ? null : assembly.GetManifestResourceStream(name);
    }

    /// <summary>
    /// The fields of <c>camera_db.meta.json</c> the plugin uses.
    /// </summary>
    private sealed class Meta
    {
        /// <summary>Gets or sets the game id, <c>ets2</c> or <c>ats</c>.</summary>
        [JsonProperty("game")]
        public string? Game { get; set; }

        /// <summary>Gets or sets the game version the map was extracted from.</summary>
        [JsonProperty("game_version")]
        public string? GameVersion { get; set; }
    }
}
