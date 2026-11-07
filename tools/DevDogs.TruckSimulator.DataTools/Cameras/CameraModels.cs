using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DevDogs.TruckSimulator.DataTools.Scs;

namespace DevDogs.TruckSimulator.DataTools.Cameras;

/// <summary>
/// What a placed enforcement model is.
/// </summary>
internal enum CameraKind
{
    /// <summary>A fining speed camera (a <c>speedcam.*</c> hookup).</summary>
    Camera,

    /// <summary>A parked police car that checks speed (a <c>police_car.*</c> hookup), the ATS speed trap.</summary>
    Trap,
}

/// <summary>
/// A unit that places a model on the map.
/// </summary>
/// <param name="Name">The unit name, for example <c>sign.ch_37007</c> or <c>model.nm_474</c>.</param>
/// <param name="ItemType">The map item type that places it: <see cref="MapCameras.SignItemType"/> or <see cref="MapCameras.ModelItemType"/>.</param>
internal sealed record PlacingUnit(
    string Name,
    uint ItemType);

/// <summary>
/// A sign or world model that enforces speed: a fining speed camera or a police speed trap.
/// </summary>
/// <param name="Model">The model path without extension, for example <c>model/sign/traffic/ch/speed_camera_ch</c>.</param>
/// <param name="Units">The <c>sign_model</c> and <c>model_def</c> units that place it, for example <c>sign.ch_37007</c>.</param>
/// <param name="Direction">
/// The travel direction the camera enforces, in degrees clockwise from the node's forward axis;
/// <see langword="null"/> when its camera locators enforce more than one direction (a two-way camera),
/// and for a trap.
/// </param>
/// <param name="Kind">Whether it is a camera or a police speed trap.</param>
internal sealed record CameraModel(
    string Model,
    IReadOnlyList<PlacingUnit> Units,
    double? Direction,
    CameraKind Kind = CameraKind.Camera);

/// <summary>
/// The outcome of looking for camera models.
/// </summary>
/// <param name="Cameras">The camera and trap models found.</param>
/// <param name="Models">How many distinct sign and world models the game defines.</param>
/// <param name="ModelsRead">How many of their <c>.pmg</c> files were on disk and read.</param>
internal sealed record CameraModelScan(
    IReadOnlyList<CameraModel> Cameras,
    int Models,
    int ModelsRead);

/// <summary>
/// Finds the models that enforce speed. ETS2 places cameras as sign items and ATS as model items, so
/// the candidates are the models of the <c>sign_model</c> units in <c>def/world/sign*.sii</c> and of the
/// <c>model_def</c> units in <c>def/world/model*.sii</c>. A model is a camera when its <c>.pmg</c> has a
/// <c>speedcam.*</c> hookup other than <c>speedcam.toll</c> (Scandinavian toll gates, not speed
/// enforcement), and a police speed trap when it has a <c>police_car.*</c> hookup instead (the parked
/// police car that checks passing speeds, <c>unit/hookup/police_car.sii</c>). Port of the DriveDogs Radar
/// <c>tools/scan_cameras.py</c>, plus the direction each camera enforces.
/// </summary>
internal static partial class CameraModels
{
    private const string SpeedCameraHookupPrefix = "speedcam.";
    private const string TollHookup = "speedcam.toll";
    private const string PoliceCarHookupPrefix = "police_car.";

    /// <summary>
    /// Locators of one model whose directions are within this many degrees enforce the same direction.
    /// </summary>
    private const double SameDirectionDegrees = 5;

    /// <summary>
    /// Turns, in degrees, for models whose camera hookup contradicts how the map places them, applied on
    /// top of <see cref="Direction"/>. Keep this short: each entry needs in-game evidence.
    /// </summary>
    /// <remarks>
    /// <c>speedcamera_small_01_it</c>: its <c>speedcam.20</c> locator puts every placed camera's trigger
    /// point on the carriageway going the other way, while <c>speedcamera_small_02_orange_it</c> has the
    /// same locator with <c>speedcam.r</c> and comes out right. In game (owner report 2026-11-01, Italian
    /// toll section) the radar alerted for the camera across the road and not for the one on the truck's
    /// side.
    /// </remarks>
    private static readonly Dictionary<string, double> DirectionCorrections = new(StringComparer.Ordinal)
    {
        ["model2/speedcamera/speedcamera_small_01_it"] = 180,
    };

    /// <summary>
    /// Scans an extracted game.
    /// </summary>
    /// <param name="baseDirectory">The extracted game root (containing <c>def</c>, <c>model</c>, <c>map</c>).</param>
    /// <returns>The camera and trap models found, sorted by model path.</returns>
    /// <exception cref="InvalidOperationException">The game defines no speed camera hookups.</exception>
    public static CameraModelScan Find(string baseDirectory)
    {
        var hookupFile = Path.Combine(baseDirectory, "unit", "hookup", "speed_camera.sii");
        if (!File.Exists(hookupFile))
        {
            throw new InvalidOperationException($"{hookupFile} not found; it says which camera hookups enforce the reverse direction.");
        }

        var reversed = ReversedHookups(File.ReadAllText(hookupFile));
        var units = UnitsByModel(Path.Combine(baseDirectory, "def", "world"));
        var cameras = new List<CameraModel>();
        var read = 0;

        foreach (var (model, modelUnits) in units.OrderBy(u => u.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(baseDirectory, model + ".pmg");
            if (!File.Exists(path))
            {
                continue;
            }

            read++;
            var bytes = File.ReadAllBytes(path);
            if (bytes.AsSpan().IndexOf("speedcam."u8) < 0 && bytes.AsSpan().IndexOf("police_car."u8) < 0)
            {
                continue;
            }

            var hookups = PmgLocators.Read(bytes).Where(l => l.Hookup is not null).ToList();
            var locators = hookups
                .Where(l => l.Hookup!.StartsWith(SpeedCameraHookupPrefix, StringComparison.Ordinal) && l.Hookup != TollHookup)
                .ToList();
            if (locators.Count > 0)
            {
                cameras.Add(new CameraModel(model, modelUnits, Corrected(model, Direction(locators, reversed))));
            }
            else if (hookups.Any(l => l.Hookup!.StartsWith(PoliceCarHookupPrefix, StringComparison.Ordinal)))
            {
                // A parked car watching a field of view, not a travel direction: it alerts from either side.
                cameras.Add(new CameraModel(model, modelUnits, null, CameraKind.Trap));
            }
        }

        return new CameraModelScan(cameras, units.Count, read);
    }

    /// <summary>
    /// The direction a camera model enforces, relative to its node's forward axis. A camera locator's
    /// forward axis looks at the traffic it measures, so it enforces the opposite heading, unless its
    /// hookup has <c>reverse_direction</c>. Calibrated on <c>speed_camera_ch</c> (ADR-0005's Berlin fines):
    /// locator turned 180°, plain hookup, enforces the node's own forward direction.
    /// </summary>
    /// <param name="locators">The model's camera locators.</param>
    /// <param name="reversed">The hookups that enforce the reverse direction.</param>
    /// <returns>Degrees clockwise from the node's forward axis, 0..360; <see langword="null"/> when the locators disagree.</returns>
    internal static double? Direction(
        IReadOnlyList<PmgLocator> locators,
        IReadOnlySet<string> reversed)
    {
        var directions = locators
            .Select(l => Normalize(MapCameras.CompassFromYawQuaternion(l.Rotation.W, l.Rotation.Y) + (reversed.Contains(l.Hookup!) ? 0 : 180)))
            .ToList();
        var first = directions[0];
        return directions.All(d => AngleBetween(d, first) <= SameDirectionDegrees) ? first : null;
    }

    /// <summary>
    /// Applies a model's entry in <see cref="DirectionCorrections"/>, if any.
    /// </summary>
    /// <param name="model">The model path without extension.</param>
    /// <param name="direction">The direction from the model's locators.</param>
    /// <returns>The corrected direction; two-way (<see langword="null"/>) stays two-way.</returns>
    internal static double? Corrected(
        string model,
        double? direction) =>
        direction is { } d && DirectionCorrections.TryGetValue(model, out var turn) ? Normalize(d + turn) : direction;

    /// <summary>
    /// Reads which <c>speed_camera</c> hookups enforce the reverse direction.
    /// </summary>
    /// <param name="sii">The content of <c>unit/hookup/speed_camera.sii</c>.</param>
    /// <returns>The hookup names with <c>reverse_direction: true</c>, for example <c>speedcam.r</c>.</returns>
    internal static IReadOnlySet<string> ReversedHookups(string sii) =>
        HookupUnitPattern().Matches(sii)
            .Where(m => ReverseDirectionPattern().IsMatch(m.Groups[2].Value))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Wraps an angle into 0..360, keeping one decimal.
    /// </summary>
    /// <param name="degrees">The angle.</param>
    /// <returns>The wrapped angle.</returns>
    internal static double Normalize(double degrees)
    {
        var wrapped = Math.Round(((degrees % 360) + 360) % 360, 1);
        return wrapped >= 360 ? 0 : wrapped;
    }

    /// <summary>
    /// The smaller angle between two headings.
    /// </summary>
    /// <param name="a">One heading.</param>
    /// <param name="b">The other heading.</param>
    /// <returns>Degrees, 0..180.</returns>
    private static double AngleBetween(
        double a,
        double b)
    {
        var delta = Math.Abs(a - b) % 360;
        return delta > 180 ? 360 - delta : delta;
    }

    /// <summary>
    /// Reads which <c>sign_model</c> and <c>model_def</c> units use which model.
    /// </summary>
    /// <param name="worldDirectory">The <c>def/world</c> folder.</param>
    /// <returns>Model path (no extension) to the units that place it.</returns>
    internal static Dictionary<string, IReadOnlyList<PlacingUnit>> UnitsByModel(string worldDirectory)
    {
        var byModel = new Dictionary<string, List<PlacingUnit>>(StringComparer.Ordinal);
        var files = Directory.GetFiles(worldDirectory, "sign*.sii")
            .Concat(Directory.GetFiles(worldDirectory, "model*.sii"))
            .OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in files)
        {
            foreach (var (unit, model) in ParseUnits(File.ReadAllText(file)))
            {
                if (!byModel.TryGetValue(model, out var list))
                {
                    byModel[model] = list = [];
                }

                list.Add(unit);
            }
        }

        return byModel.ToDictionary(e => e.Key, e => (IReadOnlyList<PlacingUnit>)e.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Extracts <c>sign_model</c> and <c>model_def</c> units and their model from SII text.
    /// </summary>
    /// <param name="sii">The SII file content.</param>
    /// <returns>Each unit with its model path (no extension, no leading slash).</returns>
    internal static IEnumerable<(PlacingUnit Unit, string Model)> ParseUnits(string sii)
    {
        foreach (Match unit in UnitPattern().Matches(sii))
        {
            var model = ModelDescPattern().Match(unit.Groups[3].Value);
            if (model.Success)
            {
                var itemType = unit.Groups[1].Value == "sign_model" ? MapCameras.SignItemType : MapCameras.ModelItemType;
                yield return (new PlacingUnit(unit.Groups[2].Value, itemType), model.Groups[1].Value);
            }
        }
    }

    [GeneratedRegex(@"^\s*(sign_model|model_def)\s*:\s*([\w.]+)\s*\{(.*?)^\s*\}", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex UnitPattern();

    [GeneratedRegex("model_desc\\s*:\\s*\"/?([^\"]+)\\.pmd\"")]
    private static partial Regex ModelDescPattern();

    [GeneratedRegex(@"^\s*speed_camera\s*:\s*([\w.]+)\s*\{(.*?)^\s*\}", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex HookupUnitPattern();

    [GeneratedRegex(@"^\s*reverse_direction\s*:\s*true\b", RegexOptions.Multiline)]
    private static partial Regex ReverseDirectionPattern();
}
