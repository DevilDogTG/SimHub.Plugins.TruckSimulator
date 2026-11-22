using DevDogs.TruckSimulator.Core.GameConfig;

namespace DevDogs.TruckSimulator.Core.Tests.GameConfig;

public class GameLogProfileTrackerTests
{
    private static GameLogProfileTracker Feed(params string[] lines)
    {
        var tracker = new GameLogProfileTracker();
        foreach (var line in lines)
        {
            tracker.Feed(line);
        }

        return tracker;
    }

    [Fact]
    public void Profile_NoProfileLines_IsNull() =>
        Feed("00:00:00.000 : [sys] Game version: 1.62.3.3s", "00:00:01.000 : Loading 'def.scs'").Profile.Should().BeNull();

    [Fact]
    public void Profile_SteamCloudProfile_NameAndCloudStorage() =>
        Feed(
            "00:00:41.120 : Set profile finished: 'Tuay'\r\n",
            "00:00:41.121 : Profile type: PC_steam_cloud\r\n",
            "00:00:41.122 : New profile selected: 'Tuay'\r\n")
            .Profile.Should().Be(new ActiveProfile("Tuay", ProfileStorage.SteamCloud));

    [Fact]
    public void Profile_LocalProfile_LocalStorage() =>
        Feed("00:00:41.120 : Set profile finished: 'Local One'", "00:00:41.121 : Profile type: PC_local")
            .Profile.Should().Be(new ActiveProfile("Local One", ProfileStorage.Local));

    [Fact]
    public void Profile_TypeLoggedBeforeName_StillPaired() =>
        Feed("00:00:41.119 : Profile type: PC_steam_cloud", "00:00:41.120 : Set profile finished: 'Tuay'")
            .Profile.Should().Be(new ActiveProfile("Tuay", ProfileStorage.SteamCloud));

    [Fact]
    public void Profile_NoTypeLine_UnknownStorage() =>
        Feed("00:00:41.120 : Set profile finished: 'Tuay'")
            .Profile.Should().Be(new ActiveProfile("Tuay", ProfileStorage.Unknown));

    [Fact]
    public void Profile_SwitchedInGame_LastProfileWins() =>
        Feed(
            "00:00:41.120 : Set profile finished: 'Tuay'",
            "00:00:41.121 : Profile type: PC_steam_cloud",
            "00:10:02.500 : Set profile finished: 'DevilDogTG'",
            "00:10:02.501 : Profile type: PC_steam_cloud")
            .Profile.Should().Be(new ActiveProfile("DevilDogTG", ProfileStorage.SteamCloud));

    [Fact]
    public void Profile_NameWithQuoteAndSpaces_KeptWhole() =>
        Feed("00:00:41.120 : Set profile finished: 'Tom's Haulage'").Profile!.Name.Should().Be("Tom's Haulage");

    [Fact]
    public void Reset_ForgetsProfile()
    {
        var tracker = Feed("00:00:41.120 : Set profile finished: 'Tuay'", "00:00:41.121 : Profile type: PC_steam_cloud");

        tracker.Reset();

        tracker.Profile.Should().BeNull();
    }
}
