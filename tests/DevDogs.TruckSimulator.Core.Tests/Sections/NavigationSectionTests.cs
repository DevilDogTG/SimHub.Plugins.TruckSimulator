using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class NavigationSectionTests
{
    [Fact]
    public void Register_DeclaresAllPartsAsZero()
    {
        var host = new FakeSectionHost();

        new NavigationSection().Register(host);

        host.Defaults.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            [NavigationSection.TotalDaysLeft] = 0,
            [NavigationSection.TotalHoursLeft] = 0,
            [NavigationSection.Minutes] = 0,
        });
    }

    [Fact]
    public void Update_TimeLeft_PublishesDaysHoursAndMinutesParts()
    {
        var host = new FakeSectionHost();
        var telemetry = new TruckTelemetry { Navigation = new NavigationState { Time = new TimeSpan(1, 5, 42, 30) } };

        new NavigationSection().Update(telemetry, DateTime.UnixEpoch, host);

        host.Properties.Should().BeEquivalentTo(new Dictionary<string, object>
        {
            [NavigationSection.TotalDaysLeft] = 1,
            [NavigationSection.TotalHoursLeft] = 5,
            [NavigationSection.Minutes] = 42,
        });
    }

    [Fact]
    public void Update_NoRoute_PublishesZeros()
    {
        var host = new FakeSectionHost();

        new NavigationSection().Update(new TruckTelemetry(), DateTime.UnixEpoch, host);

        host.Properties.Values.Should().AllBeEquivalentTo(0);
    }
}
