using System;
using System.Collections.Generic;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Radar;

/// <summary>
/// Tuning for <see cref="RadarDetector"/>. Defaults are the values proven in game by the DriveDogs
/// Radar proof of concept.
/// </summary>
public sealed record RadarConfig
{
    /// <summary>Gets the distance, in metres, at which a camera ahead starts an alert.</summary>
    public double AlertDistance { get; init; } = 350;

    /// <summary>Gets the extra distance, in metres, beyond <see cref="AlertDistance"/> an alert is kept (hysteresis).</summary>
    public double ReleaseMargin { get; init; } = 50;

    /// <summary>Gets the half-angle, in degrees, of the cone around the travel direction that counts as ahead.</summary>
    public double ConeDegrees { get; init; } = 25;

    /// <summary>Gets the height difference, in metres, beyond which a camera is on another road (bridge or underpass).</summary>
    public double MaxHeightDelta { get; init; } = 30;

    /// <summary>Gets the speed, in metres per second, below which the travel direction is not updated.</summary>
    public double MinSpeed { get; init; } = 2;

    /// <summary>Gets the distance, in metres, moved in one update that counts as a teleport (ferry, train, load).</summary>
    public double JumpDistance { get; init; } = 100;

    /// <summary>Gets the gap between updates beyond which the travel direction is forgotten (pause, load).</summary>
    public TimeSpan MaxGap { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// The detector's verdict for one update.
/// </summary>
/// <param name="Camera">The camera ahead, or <see langword="null"/> when none.</param>
/// <param name="Distance">Distance to <paramref name="Camera"/> on the ground plane, in metres; 0 when none.</param>
/// <param name="Nearest">The nearest camera in any direction (for debugging), or <see langword="null"/> when there are none.</param>
/// <param name="NearestDistance">Distance to <paramref name="Nearest"/>, in metres; 0 when none.</param>
public sealed record RadarReading(
    SpeedCamera? Camera,
    double Distance,
    SpeedCamera? Nearest,
    double NearestDistance)
{
    /// <summary>Gets a reading with nothing detected.</summary>
    public static RadarReading None { get; } = new(null, 0, null, 0);
}

/// <summary>
/// Decides whether a speed camera is ahead of the truck. The travel direction comes from successive
/// positions rather than the game's heading convention, so it behaves the same in tests and in game.
/// Port of the DriveDogs Radar <c>radar_core.hpp</c> detector, plus a direction reset after jumps.
/// </summary>
/// <param name="cameras">The cameras to watch for.</param>
public sealed class RadarDetector(IReadOnlyList<SpeedCamera> cameras)
{
    /// <summary>Smoothing weight of the latest movement, so one jittery update cannot flip the direction.</summary>
    private const double DirectionSmoothing = 0.3;

    private bool _hasPrevious;
    private WorldPosition _previous = new();
    private DateTime _previousAt;
    private bool _hasDirection;
    private double _directionX;
    private double _directionZ;
    private SpeedCamera? _alerting;

    /// <summary>
    /// Feeds one position and returns what is ahead.
    /// </summary>
    /// <param name="position">The truck's world position.</param>
    /// <param name="at">The time of the position.</param>
    /// <param name="config">The tuning to use; read on every update so setting changes apply at once.</param>
    /// <returns>The reading.</returns>
    public RadarReading Update(
        WorldPosition position,
        DateTime at,
        RadarConfig config)
    {
        UpdateDirection(position, at, config);

        var nearest = default(SpeedCamera);
        var nearestDistance = double.MaxValue;
        var ahead = default(SpeedCamera);
        var limit = _alerting is null ? config.AlertDistance : config.AlertDistance + config.ReleaseMargin;
        var aheadDistance = limit;
        var cosCone = Math.Cos(config.ConeDegrees * Math.PI / 180);

        foreach (var camera in cameras)
        {
            var vx = camera.X - position.X;
            var vz = camera.Z - position.Z;
            var distance = Math.Sqrt(vx * vx + vz * vz);

            if (distance < nearestDistance)
            {
                nearest = camera;
                nearestDistance = distance;
            }

            if (!_hasDirection
                || distance >= aheadDistance
                || distance < 1e-3
                || Math.Abs(camera.Y - position.Y) > config.MaxHeightDelta
                || (vx * _directionX + vz * _directionZ) / distance < cosCone)
            {
                continue;
            }

            ahead = camera;
            aheadDistance = distance;
        }

        _alerting = ahead;

        return new RadarReading(
            ahead,
            ahead is null ? 0 : aheadDistance,
            nearest,
            nearest is null ? 0 : nearestDistance);
    }

    /// <summary>
    /// Updates the smoothed travel direction from the movement since the last update, and forgets it
    /// after a teleport or a long gap so the old direction is never applied to the new location.
    /// </summary>
    /// <param name="position">The truck's world position.</param>
    /// <param name="at">The time of the position.</param>
    /// <param name="config">The tuning to use.</param>
    private void UpdateDirection(
        WorldPosition position,
        DateTime at,
        RadarConfig config)
    {
        if (_hasPrevious)
        {
            var dx = position.X - _previous.X;
            var dz = position.Z - _previous.Z;
            var moved = Math.Sqrt(dx * dx + dz * dz);
            var dt = (at - _previousAt).TotalSeconds;

            if (moved > config.JumpDistance || at - _previousAt > config.MaxGap)
            {
                _hasDirection = false;
                _directionX = 0;
                _directionZ = 0;
                _alerting = null;
            }
            else if (dt > 0 && moved / dt >= config.MinSpeed)
            {
                _directionX = ((1 - DirectionSmoothing) * _directionX) + (DirectionSmoothing * dx / moved);
                _directionZ = ((1 - DirectionSmoothing) * _directionZ) + (DirectionSmoothing * dz / moved);
                var length = Math.Sqrt(_directionX * _directionX + _directionZ * _directionZ);
                if (length > 1e-6)
                {
                    _directionX /= length;
                    _directionZ /= length;
                    _hasDirection = true;
                }
            }
        }

        _previous = position;
        _previousAt = at;
        _hasPrevious = true;
    }
}
