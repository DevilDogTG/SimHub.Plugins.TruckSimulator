using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class JobStatusSectionTests
{
    private static readonly DateTime _start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>One update: the job's cargo (empty for no job), route distance and speed limit.</summary>
    private sealed record Tick(
        string Cargo,
        float Distance = 0f,
        float SpeedLimit = 0f,
        double AtSeconds = 0d,
        bool OnJob = false);

    /// <summary>Drives the section through the ticks and returns what it published.</summary>
    private static FakeSectionHost Drive(
        JobStatusSection section,
        params Tick[] ticks)
    {
        var host = new FakeSectionHost();
        section.Register(host);

        foreach (var tick in ticks)
        {
            var telemetry = new TruckTelemetry
            {
                Job = new JobState { CargoId = tick.Cargo, CitySourceId = tick.Cargo.Length == 0 ? "" : "berlin", OnJob = tick.OnJob },
                Navigation = new NavigationState { Distance = tick.Distance, SpeedLimitMph = tick.SpeedLimit },
            };
            section.Update(telemetry, _start.AddSeconds(tick.AtSeconds), host);
        }

        return host;
    }

    /// <summary>
    /// A job taken, on a short route (distances stay within the 300 m route-jump window, as consecutive
    /// 60 Hz updates do), a speed limit seen while ongoing, and the truck within 30 m of the destination.
    /// </summary>
    private static Tick[] ArrivedAtDestination =>
    [
        new("cargo.wood", 0f, 0f, 0),
        new("cargo.wood", 280f, 0f, 1),
        new("cargo.wood", 200f, 50f, 2),
        new("cargo.wood", 20f, 0f, 3),
    ];

    /// <summary>Arrived, then the route distance stayed at 0 past the grace period.</summary>
    private static Tick[] DeliveredJob =>
    [
        .. ArrivedAtDestination,
        new("cargo.wood", 0f, 0f, 4),
        new("cargo.wood", 0f, 0f, 6.5),
    ];

    [Fact]
    public void Update_NoJob_StatusNoneWithoutEvents()
    {
        var host = Drive(new JobStatusSection(), new Tick(""));

        host.Properties.Should().Contain(JobStatusSection.Status, "None");
        host.TriggeredEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_JobAppears_StatusTaken()
    {
        var host = Drive(new JobStatusSection(), new Tick("cargo.wood"));

        host.Properties.Should().Contain(JobStatusSection.Status, "Taken");
        host.TriggeredEvents.Should().Equal(JobStatusSection.JobTaken);
    }

    [Fact]
    public void Update_RouteToCargoEndsAfterGrace_StatusLoading()
    {
        var host = Drive(
            new JobStatusSection(),
            new Tick("cargo.wood", 0f, 0f, 0),
            new Tick("cargo.wood", 0f, 0f, 1),
            new Tick("cargo.wood", 0f, 0f, 3.5));

        host.Properties.Should().Contain(JobStatusSection.Status, "Loading");
        host.TriggeredEvents.Should().Equal(JobStatusSection.JobTaken, JobStatusSection.JobLoading);
    }

    [Fact]
    public void Update_RouteZeroWithinGrace_StaysTaken()
    {
        var host = Drive(
            new JobStatusSection(),
            new Tick("cargo.wood", 0f, 0f, 0),
            new Tick("cargo.wood", 0f, 0f, 1),
            new Tick("cargo.wood", 0f, 0f, 1.9));

        host.Properties.Should().Contain(JobStatusSection.Status, "Taken");
    }

    [Fact]
    public void Update_RouteStarts_StatusOngoing()
    {
        var host = Drive(new JobStatusSection(), new Tick("cargo.wood"), new Tick("cargo.wood", 50_000f, AtSeconds: 1));

        host.Properties.Should().Contain(JobStatusSection.Status, "Ongoing");
        host.TriggeredEvents.Should().Equal(JobStatusSection.JobTaken, JobStatusSection.JobOngoing);
    }

    [Fact]
    public void Update_RouteEndsAtDestination_StatusCompleted()
    {
        var host = Drive(new JobStatusSection(), DeliveredJob);

        host.Properties.Should().Contain(JobStatusSection.Status, "Completed");
        host.TriggeredEvents.Should().Equal(JobStatusSection.JobTaken, JobStatusSection.JobOngoing, JobStatusSection.JobCompleted);
    }

    [Fact]
    public void Update_AfterCompletedHold_ReturnsToNoneWithoutRetakingSameJob()
    {
        var host = Drive(
            new JobStatusSection(),
            [.. DeliveredJob, new Tick("cargo.wood", 0f, 0f, 9), new Tick("cargo.wood", 0f, 0f, 12)]);

        host.Properties.Should().Contain(JobStatusSection.Status, "None");
        host.TriggeredEvents.Should().HaveCount(3);
    }

    [Fact]
    public void Update_JobChangesAfterReachingDestination_StatusCompleted()
    {
        var host = Drive(
            new JobStatusSection(),
            [.. ArrivedAtDestination, new Tick("cargo.steel", 10f, 0f, 4)]);

        host.Properties.Should().Contain(JobStatusSection.Status, "Completed");
    }

    [Fact]
    public void Update_SpeedLimitSeenOnlyBeforeOngoing_JobChangeIsAbandoned()
    {
        var host = Drive(
            new JobStatusSection(),
            new Tick("cargo.wood", 0f, 0f, 0),
            new Tick("cargo.wood", 280f, 50f, 1),
            new Tick("cargo.wood", 20f, 0f, 2),
            new Tick("cargo.steel", 10f, 0f, 3));

        host.Properties.Should().Contain(JobStatusSection.Status, "Abandoned");
    }

    [Fact]
    public void Update_JobChangesBeforeDestination_StatusAbandoned()
    {
        var host = Drive(
            new JobStatusSection(),
            new Tick("cargo.wood", 0f, 0f, 0),
            new Tick("cargo.wood", 50_000f, 50f, 1),
            new Tick("cargo.steel", 40_000f, 50f, 2));

        host.Properties.Should().Contain(JobStatusSection.Status, "Abandoned");
        host.TriggeredEvents.Should().EndWith(JobStatusSection.JobAbandoned);
    }

    [Fact]
    public void Update_RouteJumpsAfterReachingDestination_StatusCompleted()
    {
        var host = Drive(
            new JobStatusSection(),
            [
                .. ArrivedAtDestination,
                new Tick("cargo.wood", 5_000f, 0f, 4),
                new Tick("cargo.wood", 5_000f, 0f, 4.1),
                new Tick("cargo.wood", 5_000f, 0f, 4.2),
            ]);

        host.Properties.Should().Contain(JobStatusSection.Status, "Completed");
        host.TriggeredEvents.Should().ContainSingle(e => e == JobStatusSection.JobCompleted);
    }

    [Fact]
    public void JobStatusReset_TriggersResetAndRetakesCurrentJob()
    {
        var section = new JobStatusSection();
        var host = Drive(section, new Tick("cargo.wood"));

        host.RunAction(JobStatusSection.JobStatusReset);
        section.Update(new TruckTelemetry { Job = new JobState { CargoId = "cargo.wood" } }, _start.AddSeconds(1), host);

        host.TriggeredEvents.Should().Equal(JobStatusSection.JobTaken, JobStatusSection.JobReset, JobStatusSection.JobTaken);
        host.Properties.Should().Contain(JobStatusSection.Status, "Taken");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_OnJob_PublishesInProgress(bool onJob)
    {
        var host = Drive(new JobStatusSection(), new Tick("cargo.wood", OnJob: onJob));

        host.Properties.Should().Contain(JobStatusSection.InProgress, onJob);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("Cargo Wood", "berlin", "cargo-wood__berlin__berlin____")]
    public void JobKey_JobIds_BuildsLowerCaseKey(
        string cargo,
        string city,
        string expected)
    {
        var key = JobStatusSection.JobKey(new JobState { CargoId = cargo, CitySourceId = city, CompanySourceId = city });

        key.Should().Be(expected);
    }
}
