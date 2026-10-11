using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevDogs.TruckSimulator.DataTools.Cameras;

namespace DevDogs.TruckSimulator.DataTools;

/// <summary>
/// Maintainer tool that generates the plugin's game data from an extracted game.
/// </summary>
internal static class Program
{
    private const string Usage = """
        usage: dotnet run --project tools/DevDogs.TruckSimulator.DataTools -- cameras
                 --base <extracted game root, e.g. ...\Extracted\base\1.61.1.1>
                 --game ets2|ats
                 --out <folder, e.g. src/DevDogs.TruckSimulator.Core/Radar/Data/ets2>
                 [--game-version <version>]     default: the --base folder name
                 [--allow-missing-models]       continue when sign model .pmg files aren't extracted
        """;

    /// <summary>
    /// Runs a command.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>0 on success.</returns>
    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "cameras" => Cameras(Options(args.Skip(1))),
                _ => Fail(Usage),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException)
        {
            return Fail(ex.Message);
        }
    }

    /// <summary>
    /// Generates the camera database for one game.
    /// </summary>
    /// <param name="options">The parsed options.</param>
    /// <returns>0 on success.</returns>
    private static int Cameras(Dictionary<string, string> options)
    {
        var baseDirectory = Required(options, "base");
        var game = Required(options, "game");
        var output = Required(options, "out");
        var gameVersion = options.GetValueOrDefault("game-version") ?? new DirectoryInfo(baseDirectory).Name;
        var mapDirectory = Path.Combine(baseDirectory, "map", game switch
        {
            "ets2" => "europe",
            "ats" => "usa",
            _ => throw new ArgumentException($"Unknown game '{game}'; use ets2 or ats."),
        });

        var scan = CameraModels.Find(baseDirectory);
        var missing = scan.SignModels - scan.ModelsRead;
        Console.WriteLine($"{scan.Cameras.Count} camera models in {scan.ModelsRead} of {scan.SignModels} sign models read");
        if (missing > 0 && !options.ContainsKey("allow-missing-models"))
        {
            return Fail(
                $"{missing} sign models have no .pmg in {baseDirectory}; a camera among them would be missed. "
                + "Extract their .pmg files, or pass --allow-missing-models to accept the result.");
        }

        if (scan.Cameras.Count == 0)
        {
            return Fail("No camera models found; is --base an extracted game with model files?");
        }

        var cameras = MapCameras.Find(mapDirectory, scan.Cameras);
        if (cameras.Count == 0)
        {
            return Fail($"No cameras placed in {mapDirectory}.");
        }

        CameraDatabaseFile.Write(output, game, gameVersion, cameras);
        var withoutFacing = cameras.Count(c => c.Facing is null);
        Console.WriteLine($"{cameras.Count} cameras placed on the map ({withoutFacing} without facing) -> {output}");
        return 0;
    }

    /// <summary>
    /// Parses <c>--name value</c> pairs and <c>--flag</c> switches.
    /// </summary>
    /// <param name="args">The arguments after the command.</param>
    /// <returns>The options by name.</returns>
    private static Dictionary<string, string> Options(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (!list[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{list[i]}'.\n{Usage}");
            }

            var name = list[i][2..];
            var hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal);
            options[name] = hasValue ? list[++i] : "";
        }

        return options;
    }

    /// <summary>
    /// Gets a required option.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="name">The option name.</param>
    /// <returns>The value.</returns>
    private static string Required(
        Dictionary<string, string> options,
        string name) =>
        options.TryGetValue(name, out var value) && value.Length > 0
            ? value
            : throw new ArgumentException($"Missing --{name}.\n{Usage}");

    /// <summary>
    /// Prints an error.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>1.</returns>
    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
