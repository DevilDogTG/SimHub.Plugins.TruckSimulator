using System;
using System.Text.RegularExpressions;

namespace DevDogs.TruckSimulator.Core.GameConfig;

/// <summary>
/// Where a game profile is stored.
/// </summary>
public enum ProfileStorage
{
    /// <summary>The log did not say; look in both places.</summary>
    Unknown = 0,

    /// <summary>Steam Cloud: <c>&lt;Steam&gt;\userdata\&lt;id&gt;\&lt;appid&gt;\remote\profiles</c>.</summary>
    SteamCloud = 1,

    /// <summary>On this PC only: <c>Documents\&lt;game&gt;\profiles</c>.</summary>
    Local = 2,
}

/// <summary>
/// The profile the game has loaded.
/// </summary>
/// <param name="Name">The profile name as the game shows it.</param>
/// <param name="Storage">Where the profile is stored.</param>
public sealed record ActiveProfile(
    string Name,
    ProfileStorage Storage);

/// <summary>
/// Follows the game's <c>game.log.txt</c>, line by line, to learn which profile is loaded. The game
/// logs <c>Set profile finished: '&lt;name&gt;'</c> and <c>Profile type: &lt;type&gt;</c> each time a
/// profile loads, including a switch during the session.
/// </summary>
public sealed class GameLogProfileTracker
{
    private const string SteamCloudType = "PC_steam_cloud";

    private static readonly Regex _profileFinished = new(
        @"Set profile finished:\s*'(.*)'\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex _profileType = new(
        @"Profile type:\s*(\S+)",
        RegexOptions.CultureInvariant);

    private string? _name;
    private string? _type;

    /// <summary>
    /// Gets the profile named by the last profile load in the log, or <see langword="null"/> when no
    /// profile has loaded yet.
    /// </summary>
    public ActiveProfile? Profile => _name is null ? null : new ActiveProfile(_name, StorageOf(_type));

    /// <summary>
    /// Reads one log line.
    /// </summary>
    /// <param name="line">The line, with or without its line ending.</param>
    public void Feed(string line)
    {
        line = line.TrimEnd('\r', '\n');

        var finished = _profileFinished.Match(line);
        if (finished.Success)
        {
            _name = finished.Groups[1].Value;
            return;
        }

        var type = _profileType.Match(line);
        if (type.Success)
        {
            _type = type.Groups[1].Value;
        }
    }

    /// <summary>
    /// Forgets the profile, for a log that started over (a new game run).
    /// </summary>
    public void Reset()
    {
        _name = null;
        _type = null;
    }

    /// <summary>
    /// Maps the log's profile type to where the profile is stored.
    /// </summary>
    /// <param name="type">The type from <c>Profile type:</c>, for example <c>PC_steam_cloud</c>.</param>
    /// <returns>The storage; <see cref="ProfileStorage.Unknown"/> when the log gave no type.</returns>
    internal static ProfileStorage StorageOf(string? type) => type switch
    {
        null => ProfileStorage.Unknown,
        _ when string.Equals(type, SteamCloudType, StringComparison.OrdinalIgnoreCase) => ProfileStorage.SteamCloud,
        _ => ProfileStorage.Local,
    };
}
