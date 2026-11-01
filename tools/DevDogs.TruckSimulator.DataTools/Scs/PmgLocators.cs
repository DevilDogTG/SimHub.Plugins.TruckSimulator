using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace DevDogs.TruckSimulator.DataTools.Scs;

/// <summary>
/// A model locator (attachment point).
/// </summary>
/// <param name="Name">The locator name.</param>
/// <param name="Hookup">The hookup attached to it, for example <c>speedcam.20</c>, or <see langword="null"/>.</param>
/// <param name="Position">The position in model space, in metres.</param>
/// <param name="Rotation">The rotation in model space.</param>
internal sealed record PmgLocator(
    string Name,
    string? Hookup,
    Vector3 Position,
    Quaternion Rotation);

/// <summary>
/// Reads the locators of a PMG 0x15 model file (the format of every base-game static model).
/// Layout ported from the DriveDogs Radar tools: header with counts at 4, offsets at 72; locators of
/// 44 bytes (<c>u64 name, 3 f32 position, f32 scale, 4 f32 rotation w,x,y,z, i32 hookup string offset</c>);
/// hookup names in a NUL-terminated string pool.
/// </summary>
internal static class PmgLocators
{
    private const int LocatorCountAt = 20;
    private const int LocatorsOffsetAt = 72 + 2 * 4;
    private const int StringsOffsetAt = 72 + 4 * 4;
    private const int LocatorSize = 44;
    private const int PositionAt = 8;
    private const int RotationAt = 24;
    private const int HookupAt = 40;

    /// <summary>
    /// Reads all locators.
    /// </summary>
    /// <param name="pmg">The file content.</param>
    /// <returns>The locators.</returns>
    /// <exception cref="InvalidOperationException">The data is not a PMG 0x15 file.</exception>
    public static IReadOnlyList<PmgLocator> Read(ReadOnlySpan<byte> pmg)
    {
        if (pmg.Length < 112 || pmg[0] != 0x15 || pmg[1] != (byte)'g' || pmg[2] != (byte)'m' || pmg[3] != (byte)'P')
        {
            throw new InvalidOperationException("Not a PMG 0x15 file.");
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(pmg[LocatorCountAt..]);
        var locators = BinaryPrimitives.ReadInt32LittleEndian(pmg[LocatorsOffsetAt..]);
        var strings = BinaryPrimitives.ReadInt32LittleEndian(pmg[StringsOffsetAt..]);
        var result = new List<PmgLocator>(count);

        for (var i = 0; i < count; i++)
        {
            var locator = pmg.Slice(locators + i * LocatorSize, LocatorSize);
            var name = ScsToken.Decode(BinaryPrimitives.ReadUInt64LittleEndian(locator));
            var hookupAt = BinaryPrimitives.ReadInt32LittleEndian(locator[HookupAt..]);
            result.Add(new PmgLocator(
                name,
                hookupAt < 0 ? null : ReadString(pmg[(strings + hookupAt)..]),
                new Vector3(Single(locator, PositionAt), Single(locator, PositionAt + 4), Single(locator, PositionAt + 8)),
                new Quaternion(Single(locator, RotationAt + 4), Single(locator, RotationAt + 8), Single(locator, RotationAt + 12), Single(locator, RotationAt))));
        }

        return result;
    }

    /// <summary>
    /// Reads a little-endian 32-bit float.
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <param name="offset">The offset of the value.</param>
    /// <returns>The value.</returns>
    private static float Single(
        ReadOnlySpan<byte> data,
        int offset) => BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);

    /// <summary>
    /// Reads a NUL-terminated ASCII string.
    /// </summary>
    /// <param name="data">The bytes starting at the string.</param>
    /// <returns>The string.</returns>
    private static string ReadString(ReadOnlySpan<byte> data)
    {
        var end = data.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? data : data[..end]);
    }
}
