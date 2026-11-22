using System;
using System.Text.RegularExpressions;

namespace DevDogs.TruckSimulator.Core.GameConfig;

/// <summary>
/// Reads values from a game profile's <c>config.cfg</c>, a plain-text file of
/// <c>uset &lt;name&gt; "&lt;value&gt;"</c> lines.
/// </summary>
public static class ProfileConfig
{
    /// <summary>The variable behind the game's "Automatic high beams" option.</summary>
    public const string AutomaticHighBeams = "g_automatic_high_beams";

    private static readonly Regex _uset = new(
        @"^\s*uset\s+(\S+)\s+""([^""]*)""",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    /// <summary>
    /// Finds a variable's value. When the variable appears more than once, the last line wins, as it
    /// does when the game reads the file.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="name">The variable name, for example <c>g_automatic_high_beams</c>.</param>
    /// <returns>The value without its quotes, or <see langword="null"/> when the file does not set it.</returns>
    public static string? FindValue(
        string text,
        string name)
    {
        string? value = null;
        foreach (Match match in _uset.Matches(text))
        {
            if (string.Equals(match.Groups[1].Value, name, StringComparison.Ordinal))
            {
                value = match.Groups[2].Value;
            }
        }

        return value;
    }

    /// <summary>
    /// Reads the "Automatic high beams" option.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <returns>
    /// <see langword="true"/> when the value is <c>"1"</c>, <see langword="false"/> for any other value,
    /// <see langword="null"/> when the file does not set it.
    /// </returns>
    public static bool? AutomaticHighBeamsOn(string text) =>
        FindValue(text, AutomaticHighBeams) is { } value ? value == "1" : null;
}
