using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevDogs.TruckSimulator.DataTools.Scs;

namespace DevDogs.TruckSimulator.DataTools.Cameras;

/// <summary>
/// A speed camera placed on the map.
/// </summary>
/// <param name="Uid">The sign item id.</param>
/// <param name="X">World X, in metres (centre of the item's bounding box).</param>
/// <param name="Y">World Y (height), in metres.</param>
/// <param name="Z">World Z, in metres.</param>
/// <param name="Unit">The sign unit, for example <c>sign.ch_37007</c>.</param>
/// <param name="Model">The model name, for example <c>speed_camera_ch</c>.</param>
/// <param name="Sector">The map sector, for example <c>sec+0002-0003</c>.</param>
/// <param name="Facing">
/// Compass heading of the sign node's forward axis, which is the travel direction the camera enforces
/// (0 = -Z north, 90 = +X east); <see langword="null"/> when the node wasn't found.
/// </param>
internal sealed record MapCamera(
    ulong Uid,
    double X,
    double Y,
    double Z,
    string Unit,
    string Model,
    string Sector,
    double? Facing);

/// <summary>
/// Finds placed cameras in the map sectors. Sign items (type 36) are found by the model token of each
/// camera's sign unit, validating the fixed item header in front of it
/// (<c>u32 type | u64 uid | f32 kdop_min[5] | f32 kdop_max[5] | u32 flags | u8 view_distance | u64 model | u64 node</c>);
/// the node record (<c>u64 uid | i32 position×256 [3] | f32 quaternion w,x,y,z</c>) gives the facing.
/// Port of the DriveDogs Radar <c>tools/extract_camera_db.py</c>, plus the facing (map format 907, 1.61).
/// </summary>
internal static class MapCameras
{
    private const uint SignItemType = 36;
    private const int TypeOffset = -57;
    private const int UidOffset = -53;
    private const int BoundsOffset = -45;
    private const int NodeOffset = 8;

    /// <summary>How close, in metres, a node record must be to the camera to be its node (the uid also appears as the item's own reference).</summary>
    private const double NodeMatchMetres = 30;

    /// <summary>
    /// Scans every sector of a map folder.
    /// </summary>
    /// <param name="mapDirectory">The map folder, for example <c>map/europe</c>.</param>
    /// <param name="models">The camera models to look for.</param>
    /// <returns>The placed cameras, sorted by sector then uid.</returns>
    public static IReadOnlyList<MapCamera> Find(
        string mapDirectory,
        IReadOnlyList<CameraModel> models)
    {
        var tokens = models
            .SelectMany(m => m.SignUnits.Select(u => (Token: ScsToken.Encode(u[(u.IndexOf('.') + 1)..]), Unit: u, Model: m.Model)))
            .ToList();
        var cameras = new Dictionary<ulong, MapCamera>();
        var files = Directory.GetFiles(mapDirectory)
            .Where(f => f.EndsWith(".base", StringComparison.Ordinal) || f.EndsWith(".aux", StringComparison.Ordinal))
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal);
        var sectorBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            var sector = Path.GetFileNameWithoutExtension(file);

            foreach (var (token, unit, model) in tokens)
            {
                foreach (var at in Occurrences(data, token))
                {
                    if (at + TypeOffset < 0
                        || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + TypeOffset)) != SignItemType)
                    {
                        continue;
                    }

                    var uid = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(at + UidOffset));
                    if (cameras.ContainsKey(uid))
                    {
                        continue;
                    }

                    var (x, y, z) = BoundsCentre(data.AsSpan(at + BoundsOffset));
                    var node = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(at + NodeOffset));
                    var facing = NodeFacing(SectorBytes(sectorBytes, mapDirectory, sector), node, x, z);
                    var modelName = model[(model.LastIndexOf('/') + 1)..];
                    cameras[uid] = new MapCamera(uid, Math.Round(x, 2), Math.Round(y, 2), Math.Round(z, 2), unit, modelName, sector, facing);
                }
            }
        }

        return cameras.Values
            .OrderBy(c => c.Sector, StringComparer.Ordinal)
            .ThenBy(c => c.Uid.ToString("x16"), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The compass heading of a node's forward axis: the node quaternion is a yaw about +Y, and the
    /// forward axis (-Z) rotated by yaw θ points at compass heading -θ.
    /// </summary>
    /// <param name="w">Quaternion w.</param>
    /// <param name="y">Quaternion y.</param>
    /// <returns>The heading in degrees, 0..360, rounded to 0.1.</returns>
    internal static double CompassFromYawQuaternion(
        float w,
        float y)
    {
        var heading = -2 * Math.Atan2(y, w) * 180 / Math.PI;
        return Math.Round(((heading % 360) + 360) % 360, 1);
    }

    /// <summary>
    /// Finds the node record next to the camera and returns its facing.
    /// </summary>
    /// <param name="sector">The sector's <c>.aux</c> and <c>.base</c> bytes.</param>
    /// <param name="node">The node uid.</param>
    /// <param name="x">The camera's X.</param>
    /// <param name="z">The camera's Z.</param>
    /// <returns>The facing, or <see langword="null"/> when no matching node record exists.</returns>
    internal static double? NodeFacing(
        byte[] sector,
        ulong node,
        double x,
        double z)
    {
        foreach (var at in Occurrences(sector, node))
        {
            if (at + 36 > sector.Length)
            {
                continue;
            }

            var span = sector.AsSpan(at);
            var nodeX = BinaryPrimitives.ReadInt32LittleEndian(span[8..]) / 256.0;
            var nodeZ = BinaryPrimitives.ReadInt32LittleEndian(span[16..]) / 256.0;
            if (Math.Abs(nodeX - x) < NodeMatchMetres && Math.Abs(nodeZ - z) < NodeMatchMetres)
            {
                var w = BinaryPrimitives.ReadSingleLittleEndian(span[20..]);
                var y = BinaryPrimitives.ReadSingleLittleEndian(span[28..]);
                return CompassFromYawQuaternion(w, y);
            }
        }

        return null;
    }

    /// <summary>
    /// The centre of an item's bounding box (first three of five kdop axes = world x, y, z).
    /// </summary>
    /// <param name="bounds">The kdop minimums followed by the maximums.</param>
    /// <returns>The centre.</returns>
    private static (double X, double Y, double Z) BoundsCentre(ReadOnlySpan<byte> bounds)
    {
        // Sum as double, like the Python original, so rounded positions match it exactly.
        var centre = new double[3];
        for (var i = 0; i < 3; i++)
        {
            centre[i] = ((double)BinaryPrimitives.ReadSingleLittleEndian(bounds[(i * 4)..])
                + BinaryPrimitives.ReadSingleLittleEndian(bounds[(20 + i * 4)..])) / 2.0;
        }

        return (centre[0], centre[1], centre[2]);
    }

    /// <summary>
    /// Loads (once) a sector's <c>.aux</c> and <c>.base</c> bytes, which hold its node records.
    /// </summary>
    /// <param name="cache">Already loaded sectors.</param>
    /// <param name="mapDirectory">The map folder.</param>
    /// <param name="sector">The sector name.</param>
    /// <returns>The bytes.</returns>
    private static byte[] SectorBytes(
        Dictionary<string, byte[]> cache,
        string mapDirectory,
        string sector)
    {
        if (!cache.TryGetValue(sector, out var bytes))
        {
            bytes = new[] { ".aux", ".base" }
                .Select(ext => Path.Combine(mapDirectory, sector + ext))
                .Where(File.Exists)
                .SelectMany(File.ReadAllBytes)
                .ToArray();
            cache[sector] = bytes;
        }

        return bytes;
    }

    /// <summary>
    /// Every offset at which a 64-bit little-endian value occurs.
    /// </summary>
    /// <param name="data">The bytes to search.</param>
    /// <param name="value">The value.</param>
    /// <returns>The offsets.</returns>
    private static IEnumerable<int> Occurrences(
        byte[] data,
        ulong value)
    {
        var pattern = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(pattern, value);
        var start = 0;
        while (start < data.Length)
        {
            var found = data.AsSpan(start).IndexOf(pattern);
            if (found < 0)
            {
                yield break;
            }

            yield return start + found;
            start += found + 1;
        }
    }
}
