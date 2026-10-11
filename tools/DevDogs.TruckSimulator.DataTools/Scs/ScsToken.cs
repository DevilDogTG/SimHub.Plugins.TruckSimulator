using System;
using System.Text;

namespace DevDogs.TruckSimulator.DataTools.Scs;

/// <summary>
/// SCS game tokens: names of up to 12 characters from <c>[0-9a-z_]</c> packed into a 64-bit number,
/// base 38, first character in the lowest digit.
/// </summary>
internal static class ScsToken
{
    private const string Alphabet = "\00123456789abcdefghijklmnopqrstuvwxyz_";

    /// <summary>
    /// Encodes a token.
    /// </summary>
    /// <param name="text">The token text, for example <c>ch_37007</c>.</param>
    /// <returns>The packed value.</returns>
    /// <exception cref="ArgumentException">The text is longer than 12 characters or has other characters.</exception>
    public static ulong Encode(string text)
    {
        if (text.Length > 12)
        {
            throw new ArgumentException($"Token too long: '{text}'.", nameof(text));
        }

        ulong value = 0;
        ulong weight = 1;
        foreach (var c in text)
        {
            var digit = Alphabet.IndexOf(c, 1);
            if (digit < 0)
            {
                throw new ArgumentException($"Invalid token character '{c}' in '{text}'.", nameof(text));
            }

            value += (ulong)digit * weight;
            weight *= 38;
        }

        return value;
    }

    /// <summary>
    /// Decodes a token.
    /// </summary>
    /// <param name="value">The packed value.</param>
    /// <returns>The token text.</returns>
    public static string Decode(ulong value)
    {
        var text = new StringBuilder();
        while (value != 0)
        {
            text.Append(Alphabet[(int)(value % 38)]);
            value /= 38;
        }

        return text.ToString();
    }
}
