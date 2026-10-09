using System;
using System.Collections.Generic;
using System.Linq;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Diagnostics;

/// <summary>
/// The game state seen on the latest update.
/// </summary>
/// <param name="Game">The SimHub game name, for example <c>ETS2</c>; empty when none.</param>
/// <param name="GameRunning">Whether SimHub reports the game as running.</param>
/// <param name="Paused">Whether SimHub reports the game as paused.</param>
/// <param name="HasTelemetry">Whether this plugin could read telemetry on that update.</param>
public sealed record LiveStatus(
    string Game,
    bool GameRunning,
    bool Paused,
    bool HasTelemetry);

/// <summary>
/// An event this plugin triggered.
/// </summary>
/// <param name="At">The UTC time it was triggered.</param>
/// <param name="Name">The event name, without the plugin prefix.</param>
public sealed record LiveEvent(
    DateTime At,
    string Name);

/// <summary>
/// A consistent copy of what the monitor has seen, safe to read on the UI thread.
/// </summary>
/// <param name="Status">The latest game state, or <see langword="null"/> before the first update.</param>
/// <param name="Telemetry">The latest snapshot the sections received, or <see langword="null"/> when none yet.</param>
/// <param name="Outputs">The latest value of each property this plugin published, by name.</param>
/// <param name="Events">Recent events, newest first.</param>
/// <param name="FailingSections">Sections that have thrown since SimHub started.</param>
public sealed record LiveTelemetryView(
    LiveStatus? Status,
    TruckTelemetry? Telemetry,
    IReadOnlyList<KeyValuePair<string, object>> Outputs,
    IReadOnlyList<LiveEvent> Events,
    IReadOnlyList<string> FailingSections);

/// <summary>
/// Keeps the latest values this plugin read and published, for the live telemetry tab. Records
/// nothing while <see cref="Enabled"/> is off, except section failures, which are rare and always
/// worth showing. Written from SimHub's update thread, read from the UI thread.
/// </summary>
/// <param name="eventCapacity">How many recent events to keep.</param>
public sealed class LiveTelemetryMonitor(int eventCapacity = 50)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, object> _outputs = new(StringComparer.Ordinal);
    private readonly LinkedList<LiveEvent> _events = new();
    private readonly SortedSet<string> _failingSections = new(StringComparer.Ordinal);
    private LiveStatus? _status;
    private TruckTelemetry? _telemetry;
    private volatile bool _enabled;

    /// <summary>
    /// Gets or sets a value indicating whether values are being captured. Turning it off keeps what
    /// was captured; turning it on again clears it, so stale values are never shown as current.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            lock (_gate)
            {
                if (value && !_enabled)
                {
                    _outputs.Clear();
                    _events.Clear();
                    _status = null;
                    _telemetry = null;
                }

                _enabled = value;
            }
        }
    }

    /// <summary>
    /// Records the state of one update and the snapshot the sections received.
    /// </summary>
    /// <param name="status">The game state.</param>
    /// <param name="telemetry">The snapshot, or <see langword="null"/> when none could be read.</param>
    public void OnUpdate(
        LiveStatus status,
        TruckTelemetry? telemetry)
    {
        if (!_enabled)
        {
            return;
        }

        lock (_gate)
        {
            _status = status;
            if (telemetry is not null)
            {
                _telemetry = telemetry;
            }
        }
    }

    /// <summary>
    /// Records a property value this plugin published.
    /// </summary>
    /// <param name="name">The property name, without the plugin prefix.</param>
    /// <param name="value">The value.</param>
    public void OnProperty(
        string name,
        object value)
    {
        if (!_enabled)
        {
            return;
        }

        lock (_gate)
        {
            _outputs[name] = value;
        }
    }

    /// <summary>
    /// Records an event this plugin triggered.
    /// </summary>
    /// <param name="name">The event name, without the plugin prefix.</param>
    /// <param name="at">The UTC time it was triggered.</param>
    public void OnEvent(
        string name,
        DateTime at)
    {
        if (!_enabled)
        {
            return;
        }

        lock (_gate)
        {
            _events.AddFirst(new LiveEvent(at, name));
            if (_events.Count > eventCapacity)
            {
                _events.RemoveLast();
            }
        }
    }

    /// <summary>
    /// Records that a section threw during an update.
    /// </summary>
    /// <param name="section">The section's name.</param>
    public void OnSectionFailed(string section)
    {
        lock (_gate)
        {
            _failingSections.Add(section);
        }
    }

    /// <summary>
    /// Copies everything captured so far.
    /// </summary>
    /// <returns>The copy, with outputs sorted by name.</returns>
    public LiveTelemetryView Capture()
    {
        lock (_gate)
        {
            return new LiveTelemetryView(
                _status,
                _telemetry,
                _outputs.OrderBy(o => o.Key, StringComparer.Ordinal).ToList(),
                _events.ToList(),
                _failingSections.ToList());
        }
    }
}
