using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class EngineSectionTests
{
    [Fact]
    public void Register_DeclaresStartingAsFalse()
    {
        var host = new FakeSectionHost();

        new EngineSection().Register(host);

        host.Defaults.Should().Contain(EngineSection.Starting, false);
    }

    [Theory]
    [InlineData(false, 300d, true)]
    [InlineData(false, 0d, false)]
    [InlineData(true, 700d, false)]
    public void Update_EngineStateAndRpm_PublishesStarting(
        bool engineEnabled,
        double rpm,
        bool expected)
    {
        var host = new FakeSectionHost();
        var telemetry = new TruckTelemetry { Truck = new TruckState { EngineEnabled = engineEnabled, EngineRpm = rpm } };

        new EngineSection().Update(telemetry, DateTime.UnixEpoch, host);

        host.Properties.Should().Contain(EngineSection.Starting, expected);
    }
}
