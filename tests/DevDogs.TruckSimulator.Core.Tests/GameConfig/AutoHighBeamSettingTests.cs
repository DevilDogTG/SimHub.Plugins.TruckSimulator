using DevDogs.TruckSimulator.Core.GameConfig;

namespace DevDogs.TruckSimulator.Core.Tests.GameConfig;

public class AutoHighBeamSettingTests
{
    [Theory]
    [InlineData(AutoHighBeamMode.AutoDetect, true, true)]
    [InlineData(AutoHighBeamMode.AutoDetect, false, false)]
    [InlineData(AutoHighBeamMode.AutoDetect, null, false)]
    [InlineData(AutoHighBeamMode.AlwaysOn, true, true)]
    [InlineData(AutoHighBeamMode.AlwaysOn, false, true)]
    [InlineData(AutoHighBeamMode.AlwaysOn, null, true)]
    [InlineData(AutoHighBeamMode.AlwaysOff, true, false)]
    [InlineData(AutoHighBeamMode.AlwaysOff, false, false)]
    [InlineData(AutoHighBeamMode.AlwaysOff, null, false)]
    public void Resolve_OverrideWinsAutoDetectUsesProfile(
        AutoHighBeamMode mode,
        bool? fromProfile,
        bool expected) => AutoHighBeamSetting.Resolve(mode, fromProfile).Should().Be(expected);

    [Fact]
    public void NewSettings_DefaultToAutoDetect() =>
        new PluginSettings().AutoHighBeam.Should().Be(AutoHighBeamMode.AutoDetect);

    [Fact]
    public void SavedSettingsWithoutTheField_LoadAsAutoDetect() =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<PluginSettings>("{\"OverSpeedMargin\":3}")!
            .AutoHighBeam.Should().Be(AutoHighBeamMode.AutoDetect);
}
