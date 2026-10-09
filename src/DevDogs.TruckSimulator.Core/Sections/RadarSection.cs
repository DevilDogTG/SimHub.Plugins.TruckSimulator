using System;
using System.Collections.Generic;
using DevDogs.TruckSimulator.Core.Radar;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Warns when a fining speed camera is ahead of the truck. Cameras come from the database built
/// into the plugin for the running game; a game without one simply never alerts.
/// </summary>
/// <param name="settings">Supplies the alert distance, cone angle, over-speed margin and display unit.</param>
/// <param name="databases">Returns the camera database for a game, or <see langword="null"/> when there is none.</param>
public sealed class RadarSection(
    PluginSettings settings,
    Func<TruckGame, CameraDatabase?> databases) : ITelemetrySection
{
    /// <summary>Property: a camera is ahead within the alert distance and cone.</summary>
    public const string CameraAhead = "Radar.CameraAhead";

    /// <summary>Property: distance to the camera ahead, in metres (metric) or miles (imperial); 0 when none.</summary>
    public const string Distance = "Radar.Distance";

    /// <summary>Property: distance to the camera ahead in metres, whatever the display unit; 0 when none.</summary>
    public const string DistanceMeters = "Radar.DistanceMeters";

    /// <summary>Property: the camera model ahead, for example <c>speed_camera_ch</c>; empty when none.</summary>
    public const string CameraModel = "Radar.CameraModel";

    /// <summary>Property: a camera is ahead and the truck is above the speed limit plus the over-speed margin.</summary>
    public const string OverSpeed = "Radar.OverSpeed";

    /// <summary>Event: a camera came into the alert zone.</summary>
    public const string RadarCameraAhead = "RadarCameraAhead";

    /// <summary>Event: the camera left the alert zone (passed, or the truck turned away).</summary>
    public const string RadarCameraPassed = "RadarCameraPassed";

    /// <summary>Debug property: cameras loaded for the running game.</summary>
    public const string DebugCamerasLoaded = "Radar.Debug.CamerasLoaded";

    /// <summary>Debug property: the game version the camera database was extracted from.</summary>
    public const string DebugGameVersion = "Radar.Debug.GameVersion";

    /// <summary>Debug property: the truck's world X, in metres.</summary>
    public const string DebugTruckX = "Radar.Debug.TruckX";

    /// <summary>Debug property: the truck's world Z, in metres.</summary>
    public const string DebugTruckZ = "Radar.Debug.TruckZ";

    /// <summary>Debug property: distance to the nearest camera in any direction, in metres.</summary>
    public const string DebugNearestDistance = "Radar.Debug.NearestDistance";

    /// <summary>Debug property: model of the nearest camera in any direction.</summary>
    public const string DebugNearestModel = "Radar.Debug.NearestModel";

    /// <summary>Debug property: <see cref="RadarCameraAhead"/> events since SimHub started, to spot double-fires.</summary>
    public const string DebugAheadCount = "Radar.Debug.AheadCount";

    /// <summary>Debug property: <see cref="RadarCameraPassed"/> events since SimHub started.</summary>
    public const string DebugPassedCount = "Radar.Debug.PassedCount";

    private const double MetresPerMile = 1609.344;

    private readonly Dictionary<TruckGame, (CameraDatabase? Database, RadarDetector? Detector)> _byGame = [];
    private SpeedCamera? _alerting;
    private int _aheadCount;
    private int _passedCount;

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(CameraAhead, false);
        host.AddProperty(Distance, 0d);
        host.AddProperty(DistanceMeters, 0d);
        host.AddProperty(CameraModel, "");
        host.AddProperty(OverSpeed, false);
        host.AddEvent(RadarCameraAhead);
        host.AddEvent(RadarCameraPassed);
        host.AddProperty(DebugCamerasLoaded, 0);
        host.AddProperty(DebugGameVersion, "");
        host.AddProperty(DebugTruckX, 0d);
        host.AddProperty(DebugTruckZ, 0d);
        host.AddProperty(DebugNearestDistance, 0d);
        host.AddProperty(DebugNearestModel, "");
        host.AddProperty(DebugAheadCount, 0);
        host.AddProperty(DebugPassedCount, 0);
    }

    /// <summary>
    /// Switching directly from one camera to the next fires <see cref="RadarCameraPassed"/> then
    /// <see cref="RadarCameraAhead"/>, so each camera is announced.
    /// </summary>
    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var (database, detector) = ForGame(telemetry.Game);
        var reading = detector?.Update(telemetry.Truck.Position, now, Config()) ?? RadarReading.None;
        var camera = reading.Camera;

        if (!ReferenceEquals(camera, _alerting))
        {
            if (_alerting is not null)
            {
                _passedCount++;
                output.TriggerEvent(RadarCameraPassed);
            }

            if (camera is not null)
            {
                _aheadCount++;
                output.TriggerEvent(RadarCameraAhead);
            }

            _alerting = camera;
        }

        var ahead = camera is not null;
        var limit = telemetry.Navigation.SpeedLimitMph;

        output.SetProperty(CameraAhead, ahead);
        output.SetProperty(DistanceMeters, reading.Distance);
        output.SetProperty(Distance, settings.DashUnitMetric ? reading.Distance : reading.Distance / MetresPerMile);
        output.SetProperty(CameraModel, camera?.Model ?? "");
        output.SetProperty(OverSpeed, ahead && limit > 0 && telemetry.Truck.SpeedMph > limit + settings.OverSpeedMargin);

        output.SetProperty(DebugCamerasLoaded, database?.Cameras.Count ?? 0);
        output.SetProperty(DebugGameVersion, database?.GameVersion ?? "");
        output.SetProperty(DebugTruckX, telemetry.Truck.Position.X);
        output.SetProperty(DebugTruckZ, telemetry.Truck.Position.Z);
        output.SetProperty(DebugNearestDistance, reading.NearestDistance);
        output.SetProperty(DebugNearestModel, reading.Nearest?.Model ?? "");
        output.SetProperty(DebugAheadCount, _aheadCount);
        output.SetProperty(DebugPassedCount, _passedCount);
    }

    /// <summary>
    /// Builds the detector tuning from the current settings.
    /// </summary>
    /// <returns>The tuning.</returns>
    private RadarConfig Config() => new()
    {
        AlertDistance = settings.RadarAlertDistance,
        ConeDegrees = settings.RadarConeAngle,
    };

    /// <summary>
    /// Gets the database and detector for a game, loading the database on first use.
    /// </summary>
    /// <param name="game">The running game.</param>
    /// <returns>The database and detector; both <see langword="null"/> when the game has no database.</returns>
    private (CameraDatabase? Database, RadarDetector? Detector) ForGame(TruckGame game)
    {
        if (!_byGame.TryGetValue(game, out var entry))
        {
            var database = databases(game);
            entry = (database, database is null ? null : new RadarDetector(database.Cameras));
            _byGame[game] = entry;
        }

        return entry;
    }
}
