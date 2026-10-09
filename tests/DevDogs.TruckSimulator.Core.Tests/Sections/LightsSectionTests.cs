using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class LightsSectionTests
{
    private static readonly DateTime _start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TruckTelemetry Blinkers(
        bool left,
        bool right) => new() { Truck = new TruckState { BlinkerLeftOn = left, BlinkerRightOn = right } };

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Update_FirstTick_HazardOnOnlyWhenBothBlinkersOn(
        bool left,
        bool right,
        bool expected)
    {
        var host = new FakeSectionHost();

        new LightsSection().Update(Blinkers(left, right), _start, host);

        host.Properties.Should().Contain(LightsSection.HazardWarningOn, expected);
    }

    [Fact]
    public void Update_BlinkersOffWithinHold_KeepsHazardOn()
    {
        var host = new FakeSectionHost();
        var section = new LightsSection();
        section.Update(Blinkers(true, true), _start, host);

        section.Update(Blinkers(false, false), _start.AddMilliseconds(900), host);

        host.Properties.Should().Contain(LightsSection.HazardWarningOn, true);
    }

    [Fact]
    public void Update_BlinkersOffBeyondHold_TurnsHazardOff()
    {
        var host = new FakeSectionHost();
        var section = new LightsSection();
        section.Update(Blinkers(true, true), _start, host);

        section.Update(Blinkers(false, false), _start.AddMilliseconds(1100), host);

        host.Properties.Should().Contain(LightsSection.HazardWarningOn, false);
    }
}
