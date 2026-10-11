using DevDogs.TruckSimulator.Core.Radar;
using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

/// <summary>
/// Drive-through scenarios against the real ETS2 database, ported from the DriveDogs Radar harness:
/// 15 m/s through a Berlin <c>speed_camera_ch</c> (580b23, facing 260°, westbound), from 450 m out to
/// 200 m past it. By default the route follows the camera's facing, the direction it enforces.
/// </summary>
public class RadarSectionTests
{
    private static readonly DateTime _start = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CameraDatabase _ets2 = CameraDatabase.LoadEmbedded(TruckGame.Ets2)!;

    private static readonly SpeedCamera _berlinCamera = _ets2.Cameras.First(c =>
        c.Model == "speed_camera_ch" && c.X is > 8500 and < 13500 && c.Z is > -11500 and < -8000);

    /// <summary>
    /// Drives straight through the Berlin camera, recording every update's published values.
    /// </summary>
    /// <param name="heading">Compass heading of the drive; default: the camera's facing.</param>
    private static (FakeSectionHost Host, List<Dictionary<string, object>> Frames) DriveThroughBerlin(
        PluginSettings settings,
        TruckGame game = TruckGame.Ets2,
        float speedMph = 33f,
        float limitMph = 31f,
        double startMetres = 450,
        double? heading = null)
    {
        var radians = (heading ?? _berlinCamera.Facing!.Value) * Math.PI / 180;
        var (dirX, dirZ) = (Math.Sin(radians), -Math.Cos(radians));
        const double speed = 15;
        const double dt = 1.0 / 60;
        var host = new FakeSectionHost();
        var section = new RadarSection(settings, CameraDatabase.LoadEmbedded);
        section.Register(host);
        var frames = new List<Dictionary<string, object>>();

        for (var s = -startMetres; s <= 200; s += speed * dt)
        {
            var telemetry = new TruckTelemetry
            {
                Game = game,
                Truck = new TruckState
                {
                    Position = new WorldPosition { X = _berlinCamera.X + (s * dirX), Y = _berlinCamera.Y, Z = _berlinCamera.Z + (s * dirZ) },
                    SpeedMph = speedMph,
                },
                Navigation = new NavigationState { SpeedLimitMph = limitMph },
            };
            section.Update(telemetry, _start.AddSeconds((s + startMetres) / speed), host);
            frames.Add(new Dictionary<string, object>(host.Properties));
        }

        return (host, frames);
    }

    [Fact]
    public void DriveThrough_BerlinCamera_FiresAheadOnceThenPassedOnce()
    {
        var (host, _) = DriveThroughBerlin(new PluginSettings());

        host.TriggeredEvents.Should().Equal(RadarSection.RadarCameraAhead, RadarSection.RadarCameraPassed);
    }

    [Fact]
    public void DriveThrough_OppositeToCameraFacing_NeverAlerts()
    {
        // The camera fines westbound traffic only; eastbound it can't fine, so the radar stays quiet.
        // Starts 300 m out: from further back, camera 580b28 (facing 86°, eastbound) is ahead and
        // rightly alerts, since it enforces that direction.
        var (host, frames) = DriveThroughBerlin(new PluginSettings(), startMetres: 300, heading: _berlinCamera.Facing!.Value - 180);

        host.TriggeredEvents.Should().BeEmpty();
        frames.Should().OnlyContain(f => !(bool)f[RadarSection.CameraAhead]);
    }

    [Fact]
    public void DriveThrough_OppositeWithFacingFilterOff_Alerts()
    {
        var (host, _) = DriveThroughBerlin(new PluginSettings { RadarFacingFilter = false }, startMetres: 300, heading: _berlinCamera.Facing!.Value - 180);

        host.TriggeredEvents.Should().Contain(RadarSection.RadarCameraAhead);
    }

    [Fact]
    public void DriveThrough_FacingCamera_ReportsSmallFacingDelta()
    {
        var (_, frames) = DriveThroughBerlin(new PluginSettings());

        var alerting = frames.First(f => (bool)f[RadarSection.CameraAhead]);

        ((double)alerting[RadarSection.DebugNearestFacingDelta]).Should().BeLessThan(5);
    }

    [Fact]
    public void DriveThrough_BerlinCamera_AlertStartsNearAlertDistanceWithCameraModel()
    {
        var (_, frames) = DriveThroughBerlin(new PluginSettings { DashUnitMetric = true });

        var first = frames.First(f => (bool)f[RadarSection.CameraAhead]);

        ((double)first[RadarSection.Distance]).Should().BeInRange(340, 350);
        first[RadarSection.CameraModel].Should().Be(_berlinCamera.Model);
    }

    [Fact]
    public void DriveThrough_Imperial_PublishesDistanceInMiles()
    {
        var (_, frames) = DriveThroughBerlin(new PluginSettings { DashUnitMetric = false });

        var first = frames.First(f => (bool)f[RadarSection.CameraAhead]);

        ((double)first[RadarSection.Distance]).Should().BeApproximately((double)first[RadarSection.DistanceMeters] / 1609.344, 1e-9);
    }

    [Theory]
    [InlineData(35f, 31f, 3, true)]
    [InlineData(33f, 31f, 3, false)]
    [InlineData(33f, 31f, 0, true)]
    [InlineData(60f, 0f, 3, false)]
    public void DriveThrough_SpeedAgainstLimitPlusMargin_PublishesOverSpeedWhileAlerting(
        float speedMph,
        float limitMph,
        int margin,
        bool expected)
    {
        var (_, frames) = DriveThroughBerlin(new PluginSettings { OverSpeedMargin = margin }, speedMph: speedMph, limitMph: limitMph);

        var alerting = frames.First(f => (bool)f[RadarSection.CameraAhead]);

        alerting[RadarSection.OverSpeed].Should().Be(expected);
    }

    [Fact]
    public void DriveThrough_GameWithoutDatabase_NeverAlertsAndReportsNoCameras()
    {
        var (host, frames) = DriveThroughBerlin(new PluginSettings(), game: TruckGame.Ats);

        host.TriggeredEvents.Should().BeEmpty();
        frames.Should().OnlyContain(f => !(bool)f[RadarSection.CameraAhead]);
        host.Properties.Should().Contain(RadarSection.DebugCamerasLoaded, 0);
    }

    [Fact]
    public void DriveThrough_Debug_ReportsDatabaseAndTruckPosition()
    {
        var (host, _) = DriveThroughBerlin(new PluginSettings());

        host.Properties.Should().Contain(RadarSection.DebugCamerasLoaded, 707);
        host.Properties.Should().Contain(RadarSection.DebugGameVersion, "1.61.1.1");
        var radians = _berlinCamera.Facing!.Value * Math.PI / 180;
        ((double)host.Properties[RadarSection.DebugTruckX]).Should().BeApproximately(_berlinCamera.X + (200 * Math.Sin(radians)), 0.01);
        ((double)host.Properties[RadarSection.DebugTruckZ]).Should().BeApproximately(_berlinCamera.Z - (200 * Math.Cos(radians)), 0.01);
        host.Properties.Should().Contain(RadarSection.DebugAheadCount, 1);
        host.Properties.Should().Contain(RadarSection.DebugPassedCount, 1);
    }
}
