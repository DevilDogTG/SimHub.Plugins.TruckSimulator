using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DevDogs.TruckSimulator.DataTools.Scs;

namespace DevDogs.TruckSimulator.DataTools.Cameras;

/// <summary>
/// A sign model that is a fining speed camera.
/// </summary>
/// <param name="Model">The model path without extension, for example <c>model/sign/traffic/ch/speed_camera_ch</c>.</param>
/// <param name="SignUnits">The <c>sign_model</c> units that place it, for example <c>sign.ch_37007</c>.</param>
internal sealed record CameraModel(
    string Model,
    IReadOnlyList<string> SignUnits);

/// <summary>
/// The outcome of looking for camera models.
/// </summary>
/// <param name="Cameras">The camera models found.</param>
/// <param name="SignModels">How many distinct sign models the game defines.</param>
/// <param name="ModelsRead">How many of their <c>.pmg</c> files were on disk and read.</param>
internal sealed record CameraModelScan(
    IReadOnlyList<CameraModel> Cameras,
    int SignModels,
    int ModelsRead);

/// <summary>
/// Finds the sign models that are fining speed cameras. The map places cameras as sign items, so the
/// candidates are the models of the <c>sign_model</c> units in <c>def/world/sign*.sii</c>; a model is a
/// camera when its <c>.pmg</c> has a <c>speedcam.*</c> hookup other than <c>speedcam.toll</c>
/// (Scandinavian toll gates, not speed enforcement). Port of the DriveDogs Radar
/// <c>tools/scan_cameras.py</c>.
/// </summary>
internal static partial class CameraModels
{
    private const string SpeedCameraHookupPrefix = "speedcam.";
    private const string TollHookup = "speedcam.toll";

    /// <summary>
    /// Scans an extracted game.
    /// </summary>
    /// <param name="baseDirectory">The extracted game root (containing <c>def</c>, <c>model</c>, <c>map</c>).</param>
    /// <returns>The camera models found, sorted by model path.</returns>
    public static CameraModelScan Find(string baseDirectory)
    {
        var units = SignUnitsByModel(Path.Combine(baseDirectory, "def", "world"));
        var cameras = new List<CameraModel>();
        var read = 0;

        foreach (var (model, signUnits) in units.OrderBy(u => u.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(baseDirectory, model + ".pmg");
            if (!File.Exists(path))
            {
                continue;
            }

            read++;
            var bytes = File.ReadAllBytes(path);
            if (bytes.AsSpan().IndexOf("speedcam."u8) < 0)
            {
                continue;
            }

            var hookups = PmgLocators.Read(bytes).Select(l => l.Hookup).OfType<string>().ToList();
            if (hookups.Any(h => h.StartsWith(SpeedCameraHookupPrefix, StringComparison.Ordinal) && h != TollHookup))
            {
                cameras.Add(new CameraModel(model, signUnits));
            }
        }

        return new CameraModelScan(cameras, units.Count, read);
    }

    /// <summary>
    /// Reads which <c>sign_model</c> units use which model.
    /// </summary>
    /// <param name="worldDirectory">The <c>def/world</c> folder.</param>
    /// <returns>Model path (no extension) to the unit names that place it.</returns>
    internal static Dictionary<string, IReadOnlyList<string>> SignUnitsByModel(string worldDirectory)
    {
        var byModel = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var files = Directory.GetFiles(worldDirectory, "sign*.sii").OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in files)
        {
            foreach (var (unit, model) in ParseSignUnits(File.ReadAllText(file)))
            {
                if (!byModel.TryGetValue(model, out var list))
                {
                    byModel[model] = list = [];
                }

                list.Add(unit);
            }
        }

        return byModel.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Extracts <c>sign_model</c> units and their model from SII text.
    /// </summary>
    /// <param name="sii">The SII file content.</param>
    /// <returns>Each unit name with its model path (no extension, no leading slash).</returns>
    internal static IEnumerable<(string Unit, string Model)> ParseSignUnits(string sii)
    {
        foreach (Match unit in SignUnitPattern().Matches(sii))
        {
            var model = ModelDescPattern().Match(unit.Groups[2].Value);
            if (model.Success)
            {
                yield return (unit.Groups[1].Value, model.Groups[1].Value);
            }
        }
    }

    [GeneratedRegex(@"^\s*sign_model\s*:\s*([\w.]+)\s*\{(.*?)^\s*\}", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex SignUnitPattern();

    [GeneratedRegex("model_desc\\s*:\\s*\"/?([^\"]+)\\.pmd\"")]
    private static partial Regex ModelDescPattern();
}
