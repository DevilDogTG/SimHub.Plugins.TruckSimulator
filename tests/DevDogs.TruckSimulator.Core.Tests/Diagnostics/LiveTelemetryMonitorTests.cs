using DevDogs.TruckSimulator.Core.Diagnostics;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Diagnostics;

public class LiveTelemetryMonitorTests
{
    private static readonly DateTime _start = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly LiveStatus _driving = new("ETS2", GameRunning: true, Paused: false, HasTelemetry: true);

    [Fact]
    public void Capture_Disabled_RecordsNothing()
    {
        var monitor = new LiveTelemetryMonitor();

        monitor.OnUpdate(_driving, new TruckTelemetry());
        monitor.OnProperty("Job.Status", "Taken");
        monitor.OnEvent("JobTaken", _start);

        monitor.Capture().Should().BeEquivalentTo(new LiveTelemetryView(null, null, [], [], []));
    }

    [Fact]
    public void Capture_Enabled_ReturnsLatestValuesSortedByName()
    {
        var monitor = new LiveTelemetryMonitor { Enabled = true };
        var telemetry = new TruckTelemetry { Game = TruckGame.Ets2 };

        monitor.OnUpdate(_driving, telemetry);
        monitor.OnProperty("Lights.HazardWarningOn", false);
        monitor.OnProperty("Job.Status", "Taken");
        monitor.OnProperty("Job.Status", "Ongoing");
        var view = monitor.Capture();

        view.Status.Should().Be(_driving);
        view.Telemetry.Should().BeSameAs(telemetry);
        view.Outputs.Select(o => o.Key).Should().Equal("Job.Status", "Lights.HazardWarningOn");
        view.Outputs[0].Value.Should().Be("Ongoing");
    }

    [Fact]
    public void Capture_UpdateWithoutTelemetry_KeepsLastSnapshotAndUpdatesStatus()
    {
        var monitor = new LiveTelemetryMonitor { Enabled = true };
        var telemetry = new TruckTelemetry();
        var notRunning = new LiveStatus("", GameRunning: false, Paused: false, HasTelemetry: false);

        monitor.OnUpdate(_driving, telemetry);
        monitor.OnUpdate(notRunning, null);
        var view = monitor.Capture();

        view.Status.Should().Be(notRunning);
        view.Telemetry.Should().BeSameAs(telemetry);
    }

    [Fact]
    public void Capture_MoreEventsThanCapacity_KeepsNewestFirst()
    {
        var monitor = new LiveTelemetryMonitor(eventCapacity: 2) { Enabled = true };

        monitor.OnEvent("JobTaken", _start);
        monitor.OnEvent("JobOngoing", _start.AddSeconds(1));
        monitor.OnEvent("JobCompleted", _start.AddSeconds(2));

        monitor.Capture().Events.Select(e => e.Name).Should().Equal("JobCompleted", "JobOngoing");
    }

    [Fact]
    public void Enabled_TurnedBackOn_ClearsStaleValues()
    {
        var monitor = new LiveTelemetryMonitor { Enabled = true };
        monitor.OnProperty("Job.Status", "Taken");
        monitor.Enabled = false;

        monitor.Enabled = true;

        monitor.Capture().Outputs.Should().BeEmpty();
    }

    [Fact]
    public void OnSectionFailed_WhileDisabled_IsStillReported()
    {
        var monitor = new LiveTelemetryMonitor();

        monitor.OnSectionFailed("JobStatusSection");
        monitor.OnSectionFailed("JobStatusSection");

        monitor.Capture().FailingSections.Should().Equal("JobStatusSection");
    }
}
