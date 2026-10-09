using DevDogs.TruckSimulator.Core.Radar;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Radar;

/// <summary>
/// Ported from the DriveDogs Radar C++ tests (<c>plugin/tests/test_core.cpp</c>), plus the jump
/// reset and consecutive-camera cases.
/// </summary>
public class RadarDetectorTests
{
    private static readonly DateTime _start = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly RadarConfig _config = new();

    /// <summary>
    /// Drives in a straight line at 60 updates per second and returns every reading.
    /// </summary>
    private static List<RadarReading> Drive(
        RadarDetector detector,
        (double X, double Z) from,
        (double X, double Z) direction,
        double metres,
        double speed = 20,
        double startSeconds = 0)
    {
        const double dt = 1.0 / 60;
        var length = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        var readings = new List<RadarReading>();
        var step = 0;

        for (var s = 0.0; s <= metres; s += speed * dt, step++)
        {
            var position = new WorldPosition { X = from.X + direction.X / length * s, Z = from.Z + direction.Z / length * s };
            readings.Add(detector.Update(position, _start.AddSeconds(startSeconds + step * dt), _config));
        }

        return readings;
    }

    private static SpeedCamera Camera(
        double x,
        double z,
        double y = 0,
        string model = "cam") => new("uid", x, y, z, model);

    [Fact]
    public void Update_ApproachingCamera_AlertsOnlyWithinAlertDistance()
    {
        var camera = Camera(0, -1000);
        var detector = new RadarDetector([camera]);

        var farAway = Drive(detector, (0, 0), (0, -1), 600).Last();
        var close = Drive(detector, (0, -600), (0, -1), 100, startSeconds: 30.05).Last();

        farAway.Camera.Should().BeNull();
        close.Camera.Should().BeSameAs(camera);
        close.Distance.Should().BeApproximately(300, 1);
    }

    [Fact]
    public void Update_DrivingAwayFromCamera_NeverAlerts()
    {
        var detector = new RadarDetector([Camera(0, -1000)]);

        var readings = Drive(detector, (0, -900), (0, 1), 900);

        readings.Should().OnlyContain(r => r.Camera == null);
    }

    [Fact]
    public void Update_CameraOutsideCone_NeverAlerts()
    {
        var detector = new RadarDetector([Camera(60, -100)]);

        var readings = Drive(detector, (0, 0), (0, -1), 50);

        readings.Should().OnlyContain(r => r.Camera == null);
    }

    [Fact]
    public void Update_CameraOnBridgeAbove_NeverAlerts()
    {
        var detector = new RadarDetector([Camera(0, -200, y: 40)]);

        var readings = Drive(detector, (0, 0), (0, -1), 150);

        readings.Should().OnlyContain(r => r.Camera == null);
    }

    [Fact]
    public void Update_AfterPassingCamera_ReleasesAlert()
    {
        var detector = new RadarDetector([Camera(0, -300)]);

        var beforePass = Drive(detector, (0, 0), (0, -1), 280).Last();
        var afterPass = Drive(detector, (0, -280), (0, -1), 60, startSeconds: 14.05).Last();

        beforePass.Camera.Should().NotBeNull();
        afterPass.Camera.Should().BeNull();
    }

    [Fact]
    public void Update_Stationary_HasNoDirectionAndNeverAlerts()
    {
        var detector = new RadarDetector([Camera(0, -100)]);

        var readings = Enumerable.Range(0, 120)
            .Select(i => detector.Update(new WorldPosition(), _start.AddSeconds(i / 60.0), _config))
            .ToList();

        readings.Should().OnlyContain(r => r.Camera == null);
    }

    [Fact]
    public void Update_TeleportedNextToCamera_DoesNotReuseOldDirection()
    {
        var detector = new RadarDetector([Camera(5000, -100)]);
        Drive(detector, (0, 0), (0, -1), 100);

        var afterFerry = detector.Update(new WorldPosition { X = 5000, Z = 0 }, _start.AddSeconds(5.1), _config);

        afterFerry.Camera.Should().BeNull();
    }

    [Fact]
    public void Update_TwoCamerasInARow_SwitchesToTheNext()
    {
        var first = Camera(0, -300, model: "first");
        var second = Camera(0, -600, model: "second");
        var detector = new RadarDetector([first, second]);

        var readings = Drive(detector, (0, 0), (0, -1), 500);

        readings.Select(r => r.Camera).Where(c => c is not null).Distinct().Should().Equal(first, second);
    }

    [Fact]
    public void Update_AnyDirection_ReportsNearestCameraForDebugging()
    {
        var behind = Camera(0, 50, model: "behind");
        var detector = new RadarDetector([behind, Camera(0, -900)]);

        var reading = Drive(detector, (0, 0), (0, -1), 10).Last();

        reading.Nearest.Should().BeSameAs(behind);
        reading.NearestDistance.Should().BeApproximately(60, 1);
    }
}
