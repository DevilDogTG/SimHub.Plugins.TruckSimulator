using System.IO.Compression;
using System.Text;
using DevDogs.TruckSimulator.Core.Recording;
using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Recording;

public class TelemetryRecordingTests
{
    private static readonly DateTime _start = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TruckTelemetry _sample = new()
    {
        Game = TruckGame.Ats,
        NextRestStop = new TimeSpan(9, 30, 0),
        Truck = new TruckState
        {
            Id = "vehicle.peterbilt.579",
            EngineRpm = 1234.5,
            EngineEnabled = true,
            GearDashboard = 7,
            ForwardGearCount = 18,
            SpeedMph = 61.5f,
            FuelAverageConsumption = 0.42f,
            FuelRange = 812f,
            BlinkerLeftOn = true,
            Damage = new TruckDamage { Cabin = 0.01f, WheelsAverage = 0.03f },
        },
        Job = new JobState
        {
            CargoId = "cargo.lumber",
            CitySourceId = "sacramento",
            CitySource = "Sacramento",
            RemainingDeliveryTime = new TimeSpan(1, 2, 3, 0),
            OnJob = true,
        },
        Navigation = new NavigationState { Distance = 15_250f, Time = new TimeSpan(4, 10, 0), SpeedLimitMph = 65f },
    };

    /// <summary>Writes the ticks as a recording and returns its bytes.</summary>
    private static byte[] WriteRecording(params RecordedTick[] ticks)
    {
        var stream = new MemoryStream();
        using (var writer = new TelemetryRecordingWriter(stream, "0.1.0-test", _start))
        {
            Array.ForEach(ticks, writer.Write);
        }

        return stream.ToArray();
    }

    [Fact]
    public void Read_WrittenRecording_ReturnsIdenticalTicks()
    {
        RecordedTick[] ticks = [new() { At = _start, Telemetry = _sample }, new() { At = _start.AddMilliseconds(16), Telemetry = new TruckTelemetry() }];

        var read = TelemetryRecordingFormat.Read(new MemoryStream(WriteRecording(ticks))).ToList();

        read.Should().BeEquivalentTo(ticks, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Read_FieldMissingFromOlderRecording_ReadsDefault()
    {
        var bytes = Gzip(
            """{"Format":"ddtruck-telemetry","Version":1,"PluginVersion":"0.0.1","StartedAt":"2026-10-09T12:00:00Z"}""",
            """{"At":"2026-10-09T12:00:00Z","Telemetry":{"Game":"Ets2","Truck":{"Id":"vehicle.scania.r_2016"}}}""");

        var tick = TelemetryRecordingFormat.Read(new MemoryStream(bytes)).Single();

        tick.Telemetry.Game.Should().Be(TruckGame.Ets2);
        tick.Telemetry.Truck.Id.Should().Be("vehicle.scania.r_2016");
        tick.Telemetry.Job.CargoId.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{"Format":"something-else","Version":1}""")]
    [InlineData("""{"Format":"ddtruck-telemetry","Version":99}""")]
    public void Read_UnknownFormatOrNewerVersion_Throws(string header)
    {
        var read = () => TelemetryRecordingFormat.Read(new MemoryStream(Gzip(header))).ToList();

        read.Should().Throw<InvalidDataException>().WithMessage("*ddtruck-telemetry*");
    }

    [Fact]
    public void Recorder_StartRecordStop_WritesEveryTick()
    {
        var stream = new MemoryStream();
        var recorder = new TelemetryRecorder();
        recorder.Start(stream, "0.1.0-test", _start);

        recorder.Record(_start, _sample);
        recorder.Record(_start.AddMilliseconds(16), _sample);
        recorder.Stop();

        recorder.IsRecording.Should().BeFalse();
        recorder.RecordedTicks.Should().Be(2);
        TelemetryRecordingFormat.Read(new MemoryStream(stream.ToArray())).Should().HaveCount(2);
    }

    [Fact]
    public void Recorder_NotRecording_IgnoresTicks()
    {
        var recorder = new TelemetryRecorder();

        recorder.Record(_start, _sample);

        recorder.IsRecording.Should().BeFalse();
        recorder.RecordedTicks.Should().Be(0);
    }

    [Fact]
    public void RecordingSection_Toggle_StartsThenStopsRecordingToTarget()
    {
        var target = new MemoryTarget();
        var host = new Sections.FakeSectionHost();
        var section = new RecordingSection(new TelemetryRecorder(), target, "0.1.0-test");
        section.Register(host);

        host.RunAction(RecordingSection.ToggleTelemetryRecording);
        section.Update(_sample, _start, host);
        host.RunAction(RecordingSection.ToggleTelemetryRecording);

        host.Properties.Should().Contain(RecordingSection.Recording, false);
        section.Location.Should().Be("memory");
        TelemetryRecordingFormat.Read(new MemoryStream(target.Stream.ToArray())).Single().Telemetry.Should().BeEquivalentTo(_sample);
    }

    [Fact]
    public void Replay_RecordedJob_DrivesSectionsLikeLiveTelemetry()
    {
        var ticks = TelemetryRecordingFormat.Read(new MemoryStream(WriteRecording(
            new RecordedTick { At = _start, Telemetry = new TruckTelemetry { Job = new JobState { CargoId = "cargo.wood" } } },
            new RecordedTick { At = _start.AddSeconds(1), Telemetry = new TruckTelemetry { Job = new JobState { CargoId = "cargo.wood" }, Navigation = new NavigationState { Distance = 250f } } })));

        var host = TelemetryReplay.Run(new JobStatusSection(), ticks);

        host.TriggeredEvents.Should().Equal(JobStatusSection.JobTaken, JobStatusSection.JobOngoing);
    }

    private static byte[] Gzip(params string[] lines)
    {
        var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        }

        return stream.ToArray();
    }

    private sealed class MemoryTarget : IRecordingTarget
    {
        public MemoryStream Stream { get; } = new();

        public Stream Create(
            DateTime startedAt,
            out string location)
        {
            location = "memory";
            return Stream;
        }
    }
}
