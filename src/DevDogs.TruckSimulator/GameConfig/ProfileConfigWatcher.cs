using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DevDogs.TruckSimulator.Core.GameConfig;
using DevDogs.TruckSimulator.Core.Telemetry;
using Microsoft.Win32;

namespace DevDogs.TruckSimulator.GameConfig;

/// <summary>
/// Keeps the game's "Automatic high beams" option, read from the loaded profile's <c>config.cfg</c>,
/// while ETS2 or ATS runs. The profile comes from <c>game.log.txt</c> (polled once a second); the file is
/// watched and re-read about 300 ms after the game writes it, which it does as soon as Options is applied.
/// </summary>
/// <remarks>
/// All work runs on timer and watcher threads, never on SimHub's update thread; <see cref="Value"/> is
/// safe to read from any thread and never waits for file I/O. Files are opened with full sharing so the game's writes never fail.
/// </remarks>
internal sealed class ProfileConfigWatcher : IDisposable
{
    private const int Unknown = -1;

    private static readonly TimeSpan _logPoll = TimeSpan.FromSeconds(1);

    /// <summary>One Options apply writes three files within about 0.25 s; wait for the last one.</summary>
    private static readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(300);

    private readonly object _gate = new();
    private readonly GameLogProfileTracker _profile = new();
    private readonly Timer _logTimer;
    private readonly Timer _readTimer;

    private volatile TruckGame _game;
    private GameFolders? _folders;
    private long _logOffset;
    private ActiveProfile? _lastProfile;
    private string? _configPath;
    private FileSystemWatcher? _watcher;
    private bool? _value;
    private volatile int _published = Unknown;
    private bool _retried;
    private bool _errorLogged;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileConfigWatcher"/> class. It does nothing until
    /// <see cref="SetGame"/> reports a running game.
    /// </summary>
    public ProfileConfigWatcher()
    {
        _logTimer = new Timer(_ => Guard(PollLog));
        _readTimer = new Timer(_ => Guard(ReadConfig));
    }

    /// <summary>
    /// Gets the option from the loaded profile, or <see langword="null"/> when unknown (no game, no
    /// profile found, the file does not set it).
    /// </summary>
    public bool? Value => _published switch
    {
        Unknown => null,
        var v => v == 1,
    };

    /// <summary>
    /// Follows the running game. Cheap when the game has not changed; call it on every update.
    /// </summary>
    /// <param name="game">The running game, or <see cref="TruckGame.Unknown"/> when none runs.</param>
    public void SetGame(TruckGame game)
    {
        if (game == _game)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed || game == _game)
            {
                return;
            }

            Stop();
            _game = game;
            _folders = GameFolders.For(game, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), SteamRoot());
            if (_folders is null)
            {
                return;
            }

            SimHub.Logging.Current.Info($"DevDogs.TruckSimulator: watching {game} profiles (log {_folders.GameLog}, Steam {_folders.SteamRoot ?? "not found"})");
            _logTimer.Change(TimeSpan.Zero, _logPoll);
        }
    }

    /// <summary>
    /// Stops watching.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        _logTimer.Dispose();
        _readTimer.Dispose();
    }

    /// <summary>
    /// Reads the Steam install folder from the registry.
    /// </summary>
    /// <returns>The folder, or <see langword="null"/> when Steam is not installed.</returns>
    private static string? SteamRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") is string path && path.Length > 0
            ? Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar))
            : null;
    }

    /// <summary>
    /// Opens a file the game may be writing, without blocking it.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The open stream.</returns>
    private static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>
    /// Forgets the game and its profile. Caller holds <see cref="_gate"/>.
    /// </summary>
    private void Stop()
    {
        _logTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _readTimer.Change(Timeout.Infinite, Timeout.Infinite);
        WatchConfig(null);
        _game = TruckGame.Unknown;
        _folders = null;
        _logOffset = 0;
        _profile.Reset();
        _lastProfile = null;
        Publish(null);
    }

    /// <summary>
    /// Reads the new lines of <c>game.log.txt</c> and follows a profile change.
    /// </summary>
    private void PollLog()
    {
        if (_folders is null)
        {
            return;
        }

        ReadNewLogLines(_folders.GameLog);

        var profile = _profile.Profile;
        if (profile != _lastProfile || _configPath is null)
        {
            if (profile != _lastProfile)
            {
                SimHub.Logging.Current.Info($"DevDogs.TruckSimulator: profile {(profile is null ? "not in game.log yet" : $"'{profile.Name}' ({profile.Storage})")}");
            }

            _lastProfile = profile;
            WatchConfig(ProfileConfigLocator.Find(_folders, profile, DiskProbe.Instance));
        }
    }

    /// <summary>
    /// Feeds the complete lines written since the last poll to the profile tracker. The log starts over
    /// on each game run; a log shorter than what was read means a new run.
    /// </summary>
    /// <param name="path">The log file.</param>
    private void ReadNewLogLines(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = OpenShared(path);
        if (stream.Length < _logOffset)
        {
            _logOffset = 0;
            _profile.Reset();
        }

        if (stream.Length == _logOffset)
        {
            return;
        }

        stream.Position = _logOffset;
        var bytes = new byte[stream.Length - _logOffset];
        var read = 0;
        while (read < bytes.Length)
        {
            var n = stream.Read(bytes, read, bytes.Length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        if (read == 0)
        {
            return;
        }

        // Only whole lines: a line still being written is read on the next poll.
        var end = Array.LastIndexOf(bytes, (byte)'\n', read - 1);
        if (end < 0)
        {
            return;
        }

        _logOffset += end + 1;
        foreach (var line in Encoding.UTF8.GetString(bytes, 0, end + 1).Split('\n'))
        {
            _profile.Feed(line);
        }
    }

    /// <summary>
    /// Switches the watched file and reads it in full. Caller holds <see cref="_gate"/>.
    /// </summary>
    /// <param name="configPath">The profile's <c>config.cfg</c>, or <see langword="null"/> for none.</param>
    private void WatchConfig(string? configPath)
    {
        if (string.Equals(configPath, _configPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _watcher?.Dispose();
        _watcher = null;
        _configPath = configPath;
        if (configPath is null)
        {
            Publish(null);
            return;
        }

        SimHub.Logging.Current.Info($"DevDogs.TruckSimulator: reading automatic high beams from {configPath}");
        var watcher = new FileSystemWatcher(Path.GetDirectoryName(configPath), Path.GetFileName(configPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };
        watcher.Changed += (_, _) => ScheduleRead();
        watcher.Created += (_, _) => ScheduleRead();
        watcher.Renamed += (_, _) => ScheduleRead();
        watcher.Deleted += (_, _) => ScheduleRead();
        watcher.Error += (_, _) => ScheduleRead();
        watcher.EnableRaisingEvents = true;
        _watcher = watcher;

        _retried = false;
        ReadConfig();
    }

    /// <summary>
    /// Reads the profile's file after the burst of writes settles; every new write restarts the wait.
    /// </summary>
    private void ScheduleRead()
    {
        lock (_gate)
        {
            if (!_disposed && _configPath is not null)
            {
                _retried = false;
                _readTimer.Change(_debounce, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    /// Reads the option from the watched file. A read that fails while the game holds the file is tried
    /// once more after the debounce. Caller holds <see cref="_gate"/>.
    /// </summary>
    private void ReadConfig()
    {
        if (_configPath is null)
        {
            return;
        }

        try
        {
            if (!File.Exists(_configPath))
            {
                Publish(null);
                return;
            }

            string text;
            using (var reader = new StreamReader(OpenShared(_configPath), Encoding.UTF8))
            {
                text = reader.ReadToEnd();
            }

            Publish(ProfileConfig.AutomaticHighBeamsOn(text));
        }
        catch (IOException) when (!_retried)
        {
            _retried = true;
            _readTimer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Keeps a new value; the game rewrites the file unchanged at profile load, game load and quit, so
    /// only a real change is logged. Caller holds <see cref="_gate"/>.
    /// </summary>
    /// <param name="value">The value read.</param>
    private void Publish(bool? value)
    {
        if (value == _value)
        {
            return;
        }

        _value = value;
        _published = value is null ? Unknown : value.Value ? 1 : 0;
        SimHub.Logging.Current.Info($"DevDogs.TruckSimulator: game automatic high beams {(value is null ? "unknown" : value.Value ? "on" : "off")}");
    }

    /// <summary>
    /// Runs timer work under the lock. An exception on a timer thread would end SimHub, so it is logged
    /// once and swallowed; the next tick tries again.
    /// </summary>
    /// <param name="work">The work.</param>
    private void Guard(Action work)
    {
        try
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    work();
                }
            }
        }
        catch (Exception ex)
        {
            if (!_errorLogged)
            {
                _errorLogged = true;
                SimHub.Logging.Current.Error("DevDogs.TruckSimulator: reading the game profile failed", ex);
            }
        }
    }

    /// <summary>
    /// The real file system for <see cref="ProfileConfigLocator"/>.
    /// </summary>
    private sealed class DiskProbe : IFileSystemProbe
    {
        public static readonly DiskProbe Instance = new();

        /// <inheritdoc />
        public IEnumerable<string> GetDirectories(string path)
        {
            try
            {
                return Directory.Exists(path) ? Directory.GetDirectories(path) : Enumerable.Empty<string>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Enumerable.Empty<string>();
            }
        }

        /// <inheritdoc />
        public DateTime? GetLastWriteTimeUtc(string path) =>
            File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
    }
}
