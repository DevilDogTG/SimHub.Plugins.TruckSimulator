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
    public void Update_PausedWithoutMoving_KeepsAlertForSameCamera()
    {
        // Real drive: a 44 s pause next to a camera released the alert and re-announced it.
        var camera = Camera(0, -300);
        var detector = new RadarDetector([camera]);
        var beforePause = Drive(detector, (0, 0), (0, -1), 100).Last();

        var afterPause = detector.Update(new WorldPosition { Z = -100 }, _start.AddSeconds(50), _config);
        var movingAgain = Drive(detector, (0, -100), (0, -1), 20, startSeconds: 50.02);

        beforePause.Camera.Should().BeSameAs(camera);
        afterPause.Camera.Should().BeSameAs(camera);
        movingAgain.Should().OnlyContain(r => r.Camera == camera);
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

    /// <summary>
    /// The cameras a drive alerted for, in order, with consecutive repeats collapsed — one entry per
    /// announcement. <see langword="null"/> entries are gaps with no alert.
    /// </summary>
    private static List<SpeedCamera?> Announcements(IEnumerable<RadarReading> readings)
    {
        var announcements = new List<SpeedCamera?>();
        foreach (var camera in readings.Select(r => r.Camera))
        {
            if (announcements.Count == 0 || !ReferenceEquals(announcements[^1], camera))
            {
                announcements.Add(camera);
            }
        }

        return announcements;
    }

    [Fact]
    public void Update_RoadsideCamera_HoldsAlertUntilAlongsideInsteadOfAtConeEdge()
    {
        // Berlin poles stand ~20 m off the road; such a camera leaves a 25° cone 43 m before it is
        // reached. Found in a real drive (2026-10-10): "passed" fired 40–50 m early.
        var roadside = Camera(20, -400);
        var detector = new RadarDetector([roadside]);

        var readings = Drive(detector, (0, 0), (0, -1), 450);
        var lastAlert = readings.FindLastIndex(r => r.Camera is not null);

        Announcements(readings).Should().Equal(null, roadside, null);
        readings[lastAlert].Distance.Should().BeLessThan(25);
    }

    [Fact]
    public void Update_TwoRoadsideCamerasInARow_AnnouncesEachOnceWithoutFlipping()
    {
        // Real drive: alternated six times in six seconds between a camera 50 m ahead at the cone's
        // edge and the next one 290 m further on.
        var first = Camera(20, -300, model: "first");
        var second = Camera(20, -560, model: "second");
        var detector = new RadarDetector([first, second]);

        var readings = Drive(detector, (0, 0), (0, -1), 600, speed: 5);

        Announcements(readings).Where(c => c is not null).Should().Equal(first, second);
    }

    [Fact]
    public void Update_NearerCameraComesIntoView_TakesOverFromFartherOne()
    {
        // Real drive (2026-10-10, dev.6): a camera 89 m ahead inside the cone was ignored while the
        // radar held one 326 m away, and that nearer camera fined the driver.
        var far = Camera(0, -330, model: "far");
        var near = Camera(-60, -80, model: "near");
        var detector = new RadarDetector([far, near]);
        var straight = Drive(detector, (0, 0), (0, -1), 10).Last();

        var bearingLeft = Drive(detector, (0, -10), (-0.36, -1), 12, startSeconds: 0.55).Last();

        straight.Camera.Should().BeSameAs(far);
        bearingLeft.Camera.Should().BeSameAs(near);
    }

    [Fact]
    public void Update_TurningAwayFromCamera_ReleasesAlert()
    {
        var camera = Camera(0, -400);
        var detector = new RadarDetector([camera]);
        var approach = Drive(detector, (0, 0), (0, -1), 150).Last();

        var afterTurn = Drive(detector, (0, -150), (1, 0), 60, startSeconds: 7.55).Last();

        approach.Camera.Should().BeSameAs(camera);
        afterTurn.Camera.Should().BeNull();
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
