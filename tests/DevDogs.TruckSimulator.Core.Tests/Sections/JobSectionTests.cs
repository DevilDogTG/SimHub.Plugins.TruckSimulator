using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class JobSectionTests
{
    private static TruckTelemetry Driving(
        float speedMph,
        float limitMph) => new()
    {
        Truck = new TruckState { SpeedMph = speedMph },
        Navigation = new NavigationState { SpeedLimitMph = limitMph },
        NextRestStop = TimeSpan.FromHours(5),
    };

    private static FakeSectionHost Run(
        TruckTelemetry telemetry,
        int overSpeedMargin = 3)
    {
        var host = new FakeSectionHost();
        new JobSection(new PluginSettings { OverSpeedMargin = overSpeedMargin }).Update(telemetry, DateTime.UnixEpoch, host);
        return host;
    }

    [Theory]
    [InlineData(53f, false)]
    [InlineData(53.5f, true)]
    [InlineData(40f, false)]
    public void Update_SpeedAgainstLimitPlusMargin_PublishesOverSpeed(
        float speed,
        bool expected)
    {
        var host = Run(Driving(speed, 50f));

        host.Properties.Should().Contain(JobSection.OverSpeedLimit, expected);
    }

    [Fact]
    public void Update_NoSpeedLimit_NeverOverSpeed()
    {
        var host = Run(Driving(90f, 0f));

        host.Properties.Should().Contain(JobSection.OverSpeedLimit, false);
        host.Properties.Should().Contain(JobSection.OverSpeedLimitPercentage, 0f);
    }

    [Theory]
    [InlineData(50f, 0f)]
    [InlineData(51.5f, 0.5f)]
    [InlineData(60f, 1f)]
    public void Update_PositiveMargin_PercentageRampsFromLimitToThreshold(
        float speed,
        float expected)
    {
        var host = Run(Driving(speed, 50f));

        ((float)host.Properties[JobSection.OverSpeedLimitPercentage]).Should().BeApproximately(expected, 0.0001f);
    }

    [Theory]
    [InlineData(48f, 0f)]
    [InlineData(49f, 0.5f)]
    [InlineData(50f, 1f)]
    public void Update_NegativeMargin_PercentageRampsFromThresholdToLimit(
        float speed,
        float expected)
    {
        var host = Run(Driving(speed, 50f), overSpeedMargin: -2);

        ((float)host.Properties[JobSection.OverSpeedLimitPercentage]).Should().BeApproximately(expected, 0.0001f);
    }

    [Theory]
    [InlineData(0, 59, true)]
    [InlineData(1, 0, false)]
    [InlineData(24, 30, false)]
    public void Update_TimeUntilRest_WarnsUnderOneHourTotal(
        int hours,
        int minutes,
        bool expected)
    {
        var host = Run(Driving(0f, 0f) with { NextRestStop = new TimeSpan(hours, minutes, 0) });

        host.Properties.Should().Contain(JobSection.NextRestWarning, expected);
    }

    [Fact]
    public void Update_RemainingDeliveryTime_PublishesParts()
    {
        var telemetry = Driving(0f, 0f) with { Job = new JobState { RemainingDeliveryTime = new TimeSpan(2, 7, 15, 0) } };

        var host = Run(telemetry);

        host.Properties.Should().Contain(JobSection.RemainingDays, 2);
        host.Properties.Should().Contain(JobSection.RemainingHours, 7);
        host.Properties.Should().Contain(JobSection.RemainingMinutes, 15);
    }
}
