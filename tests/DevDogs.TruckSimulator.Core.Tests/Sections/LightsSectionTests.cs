using DevDogs.TruckSimulator.Core.GameConfig;
using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class LightsSectionTests
{
    private static readonly DateTime _start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TruckTelemetry Blinkers(
        bool left,
        bool right) => new() { Truck = new TruckState { BlinkerLeftOn = left, BlinkerRightOn = right } };

    private static LightsSection NewSection(
        AutoHighBeamMode mode = AutoHighBeamMode.AutoDetect,
        bool? fromProfile = null) => new(new PluginSettings { AutoHighBeam = mode }, () => fromProfile);

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

        NewSection().Update(Blinkers(left, right), _start, host);

        host.Properties.Should().Contain(LightsSection.HazardWarningOn, expected);
    }

    [Fact]
    public void Update_BlinkersOffWithinHold_KeepsHazardOn()
    {
        var host = new FakeSectionHost();
        var section = NewSection();
        section.Update(Blinkers(true, true), _start, host);

        section.Update(Blinkers(false, false), _start.AddMilliseconds(900), host);

        host.Properties.Should().Contain(LightsSection.HazardWarningOn, true);
    }

    [Fact]
    public void Update_BlinkersOffBeyondHold_TurnsHazardOff()
    {
        var host = new FakeSectionHost();
        var section = NewSection();
        section.Update(Blinkers(true, true), _start, host);

        section.Update(Blinkers(false, false), _start.AddMilliseconds(1100), host);

        host.Properties.Should().Contain(LightsSection.HazardWarningOn, false);
    }

    [Fact]
    public void Register_DeclaresAutoHighBeamAsFalse()
    {
        var host = new FakeSectionHost();

        NewSection().Register(host);

        host.Defaults.Should().Contain(LightsSection.AutoHighBeam, false);
    }

    [Theory]
    [InlineData(AutoHighBeamMode.AutoDetect, true, true)]
    [InlineData(AutoHighBeamMode.AutoDetect, false, false)]
    [InlineData(AutoHighBeamMode.AutoDetect, null, false)]
    [InlineData(AutoHighBeamMode.AlwaysOn, false, true)]
    [InlineData(AutoHighBeamMode.AlwaysOn, null, true)]
    [InlineData(AutoHighBeamMode.AlwaysOff, true, false)]
    public void Update_PublishesAutoHighBeamFromSettingAndProfile(
        AutoHighBeamMode mode,
        bool? fromProfile,
        bool expected)
    {
        var host = new FakeSectionHost();

        NewSection(mode, fromProfile).Update(Blinkers(false, false), _start, host);

        host.Properties.Should().Contain(LightsSection.AutoHighBeam, expected);
    }

    [Fact]
    public void Update_SettingChanged_AppliesOnNextTick()
    {
        var host = new FakeSectionHost();
        var settings = new PluginSettings();
        var section = new LightsSection(settings, () => false);
        section.Update(Blinkers(false, false), _start, host);

        settings.AutoHighBeam = AutoHighBeamMode.AlwaysOn;
        section.Update(Blinkers(false, false), _start.AddMilliseconds(20), host);

        host.Properties.Should().Contain(LightsSection.AutoHighBeam, true);
    }
}
