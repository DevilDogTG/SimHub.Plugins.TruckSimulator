using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DevDogs.TruckSimulator.Core.Localisation;

/// <summary>
/// Converts text to ASCII for displays that can only show ASCII characters.
/// </summary>
public static class AsciiText
{
    /// <summary>
    /// Latin letters that don't decompose into a base letter plus accents.
    /// </summary>
    private static readonly Dictionary<char, string> _specialLetters = new()
    {
        ['ß'] = "ss",
        ['æ'] = "ae",
        ['Æ'] = "AE",
        ['œ'] = "oe",
        ['Œ'] = "OE",
        ['ø'] = "o",
        ['Ø'] = "O",
        ['ł'] = "l",
        ['Ł'] = "L",
        ['đ'] = "d",
        ['Đ'] = "D",
        ['ð'] = "d",
        ['Ð'] = "D",
        ['þ'] = "th",
        ['Þ'] = "Th",
        ['ı'] = "i",
    };

    /// <summary>
    /// Removes accents and replaces special Latin letters, for example <c>Kraków</c> becomes
    /// <c>Krakow</c> and <c>Łódź</c> becomes <c>Lodz</c>. Characters with no ASCII form, such as
    /// Cyrillic or CJK, are dropped.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The ASCII-only text.</returns>
    public static string Fold(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var builder = new StringBuilder(text.Length);

        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (c < 128)
            {
                builder.Append(c);
            }
            else if (_specialLetters.TryGetValue(c, out var replacement))
            {
                builder.Append(replacement);
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator)
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }
}
