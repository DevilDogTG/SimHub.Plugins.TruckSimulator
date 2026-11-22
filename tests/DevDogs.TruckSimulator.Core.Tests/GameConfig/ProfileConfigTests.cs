using DevDogs.TruckSimulator.Core.GameConfig;

namespace DevDogs.TruckSimulator.Core.Tests.GameConfig;

public class ProfileConfigTests
{
    // Shaped like a real profile config.cfg (ETS2 1.62): CRLF lines, the light options side by side.
    private const string Config =
        "# prism3d variable config data\r\n" +
        "\r\n" +
        "uset g_lang \"en_gb\"\r\n" +
        "uset g_automatic_high_beams \"1\"\r\n" +
        "uset g_automatic_headlights \"0\"\r\n" +
        "uset g_save_format \"0\"\r\n";

    [Fact]
    public void AutomaticHighBeamsOn_ValueOne_ReturnsTrue() =>
        ProfileConfig.AutomaticHighBeamsOn(Config).Should().BeTrue();

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("2")]
    public void AutomaticHighBeamsOn_OtherValue_ReturnsFalse(string value) =>
        ProfileConfig.AutomaticHighBeamsOn($"uset g_automatic_high_beams \"{value}\"\n").Should().BeFalse();

    [Fact]
    public void AutomaticHighBeamsOn_NotSet_ReturnsNull() =>
        ProfileConfig.AutomaticHighBeamsOn("uset g_automatic_headlights \"1\"\n").Should().BeNull();

    [Fact]
    public void AutomaticHighBeamsOn_EmptyFile_ReturnsNull() =>
        ProfileConfig.AutomaticHighBeamsOn("").Should().BeNull();

    [Fact]
    public void FindValue_DoesNotMatchLongerNameWithSamePrefix() =>
        ProfileConfig.FindValue("uset g_automatic_high_beams_x \"1\"\n", ProfileConfig.AutomaticHighBeams).Should().BeNull();

    [Fact]
    public void FindValue_IgnoresCommentedOutLine() =>
        ProfileConfig.FindValue("# uset g_automatic_high_beams \"1\"\n", ProfileConfig.AutomaticHighBeams).Should().BeNull();

    [Fact]
    public void FindValue_LeadingWhitespaceAndTabs_Matches() =>
        ProfileConfig.FindValue("  uset\tg_automatic_high_beams\t\"1\"\n", ProfileConfig.AutomaticHighBeams).Should().Be("1");

    [Fact]
    public void FindValue_SetTwice_LastWins() =>
        ProfileConfig.FindValue(
            "uset g_automatic_high_beams \"1\"\nuset g_automatic_high_beams \"0\"\n",
            ProfileConfig.AutomaticHighBeams).Should().Be("0");
}
