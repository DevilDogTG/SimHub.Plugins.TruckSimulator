using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.GameConfig;

/// <summary>
/// The file-system queries <see cref="ProfileConfigLocator"/> needs, so it can be tested without disk.
/// </summary>
public interface IFileSystemProbe
{
    /// <summary>
    /// Lists the sub-folders of a folder.
    /// </summary>
    /// <param name="path">The folder.</param>
    /// <returns>Full paths of the sub-folders; empty when the folder does not exist.</returns>
    IEnumerable<string> GetDirectories(string path);

    /// <summary>
    /// Gets when a file was last written.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The time in UTC, or <see langword="null"/> when the file does not exist.</returns>
    DateTime? GetLastWriteTimeUtc(string path);
}

/// <summary>
/// Where one game keeps its files.
/// </summary>
/// <param name="DocumentsFolder">The game's folder under Documents, for example <c>Documents\Euro Truck Simulator 2</c>.</param>
/// <param name="SteamRoot">The Steam install folder, or <see langword="null"/> when Steam is not installed.</param>
/// <param name="SteamAppId">The game's Steam app id.</param>
public sealed record GameFolders(
    string DocumentsFolder,
    string? SteamRoot,
    int SteamAppId)
{
    /// <summary>Gets the game's log, which starts over on each game run.</summary>
    public string GameLog => Path.Combine(DocumentsFolder, "game.log.txt");

    /// <summary>
    /// Gets the folders of one game.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <param name="documents">The user's Documents folder.</param>
    /// <param name="steamRoot">The Steam install folder, or <see langword="null"/>.</param>
    /// <returns>The game's folders, or <see langword="null"/> for an unsupported game.</returns>
    public static GameFolders? For(
        TruckGame game,
        string documents,
        string? steamRoot) => game switch
        {
            TruckGame.Ets2 => new GameFolders(Path.Combine(documents, "Euro Truck Simulator 2"), steamRoot, 227300),
            TruckGame.Ats => new GameFolders(Path.Combine(documents, "American Truck Simulator"), steamRoot, 270880),
            _ => null,
        };
}

/// <summary>
/// Finds the <c>config.cfg</c> of the profile the game has loaded.
/// </summary>
/// <remarks>
/// Never looks in <c>steam_profiles</c> or its <c>steam_profiles(&lt;version&gt;).bak</c> backups: those
/// hold only <c>config_local.cfg</c> (no light options) or stale copies from before a game update.
/// </remarks>
public static class ProfileConfigLocator
{
    /// <summary>The profile settings file name.</summary>
    public const string ConfigFileName = "config.cfg";

    /// <summary>
    /// Gets a profile's folder name: the UTF-8 bytes of its name in hex, for example <c>Tuay</c> →
    /// <c>54756179</c>.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <returns>The folder name, upper-case. Compare it with folder names ignoring case.</returns>
    public static string FolderName(string profileName)
    {
        var bytes = Encoding.UTF8.GetBytes(profileName);
        var hex = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            hex.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }

    /// <summary>
    /// Picks the <c>config.cfg</c> to read.
    /// </summary>
    /// <param name="folders">Where the game keeps its files.</param>
    /// <param name="profile">The loaded profile, or <see langword="null"/> when the log has not named one.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <returns>
    /// The named profile's <c>config.cfg</c> (the newest when several Steam accounts have that profile);
    /// with no profile, the newest <c>config.cfg</c> of all the game's profiles. <see langword="null"/>
    /// when none exists.
    /// </returns>
    public static string? Find(
        GameFolders folders,
        ActiveProfile? profile,
        IFileSystemProbe fileSystem)
    {
        var storage = profile?.Storage ?? ProfileStorage.Unknown;
        var folderName = profile is null ? null : FolderName(profile.Name);

        string? newest = null;
        var newestTime = DateTime.MinValue;
        foreach (var profilesFolder in ProfilesFolders(folders, storage, fileSystem))
        {
            foreach (var profileFolder in fileSystem.GetDirectories(profilesFolder))
            {
                if (folderName is not null
                    && !string.Equals(Path.GetFileName(profileFolder), folderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var config = Path.Combine(profileFolder, ConfigFileName);
                if (fileSystem.GetLastWriteTimeUtc(config) is { } written && (newest is null || written > newestTime))
                {
                    newest = config;
                    newestTime = written;
                }
            }
        }

        return newest;
    }

    /// <summary>
    /// Lists the folders that hold profile folders for a storage.
    /// </summary>
    /// <param name="folders">Where the game keeps its files.</param>
    /// <param name="storage">The storage to look in; <see cref="ProfileStorage.Unknown"/> for both.</param>
    /// <param name="fileSystem">The file system.</param>
    /// <returns>The <c>profiles</c> folders.</returns>
    private static IEnumerable<string> ProfilesFolders(
        GameFolders folders,
        ProfileStorage storage,
        IFileSystemProbe fileSystem)
    {
        if (storage != ProfileStorage.Local && folders.SteamRoot is { } steamRoot)
        {
            var appId = folders.SteamAppId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var account in fileSystem.GetDirectories(Path.Combine(steamRoot, "userdata")))
            {
                yield return Path.Combine(account, appId, "remote", "profiles");
            }
        }

        if (storage != ProfileStorage.SteamCloud)
        {
            yield return Path.Combine(folders.DocumentsFolder, "profiles");
        }
    }
}
