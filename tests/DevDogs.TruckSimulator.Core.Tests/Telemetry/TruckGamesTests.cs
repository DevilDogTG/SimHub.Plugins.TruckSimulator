using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Telemetry;

public class TruckGamesTests
{
    [Theory]
    [InlineData("ETS2", TruckGame.Ets2)]
    [InlineData("ATS", TruckGame.Ats)]
    public void FromSimHubName_SupportedGame_ReturnsGame(
        string name,
        TruckGame expected)
    {
        var result = TruckGames.FromSimHubName(name);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ets2")]
    [InlineData("AssettoCorsa")]
    public void FromSimHubName_OtherName_ReturnsUnknown(string? name)
    {
        var result = TruckGames.FromSimHubName(name);

        result.Should().Be(TruckGame.Unknown);
    }

    [Fact]
    public void TruckTelemetry_Default_HasEmptyStringsAndNoNestedNulls()
    {
        var telemetry = new TruckTelemetry();

        telemetry.Truck.Id.Should().BeEmpty();
        telemetry.Truck.Damage.Should().NotBeNull();
        telemetry.Job.CargoId.Should().BeEmpty();
        telemetry.Navigation.Distance.Should().Be(0f);
    }
}
