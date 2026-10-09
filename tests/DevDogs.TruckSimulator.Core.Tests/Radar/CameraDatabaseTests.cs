using DevDogs.TruckSimulator.Core.Radar;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Radar;

public class CameraDatabaseTests
{
    private const string Ets2Meta = """{"game":"ets2","game_version":"1.61.1.1","cameras":2}""";

    [Fact]
    public void Parse_ValidRows_ReadsCamerasAndVersion()
    {
        var csv = new StringReader("""
            uid,x,y,z,unit,model,sector
            573123a4820f0ae2,2871.53,44.03,13260.55,sign.ch_37008,speed_camera_v2_ch,sec+0000+0003
            01567d2460500f89,916.5,38.89,25697.07,sign.dlc_it_318,speedcamera_small_01_it,sec+0000+0006
            """);

        var database = CameraDatabase.Parse(TruckGame.Ets2, Ets2Meta, csv);

        database.GameVersion.Should().Be("1.61.1.1");
        database.Cameras.Should().HaveCount(2);
        database.Cameras[0].Should().Be(new SpeedCamera("573123a4820f0ae2", 2871.53, 44.03, 13260.55, "speed_camera_v2_ch"));
    }

    [Fact]
    public void Parse_MalformedRows_AreSkipped()
    {
        var csv = new StringReader("""
            uid,x,y,z,unit,model,sector
            short,1,2
            bad,x,y,z,unit,model,sector
            ok,1,2,3,unit,model,sector
            """);

        var database = CameraDatabase.Parse(TruckGame.Ets2, Ets2Meta, csv);

        database.Cameras.Select(c => c.Uid).Should().Equal("ok");
    }

    [Fact]
    public void Parse_MetadataForOtherGame_Throws()
    {
        var parse = () => CameraDatabase.Parse(TruckGame.Ats, Ets2Meta, new StringReader(""));

        parse.Should().Throw<InvalidDataException>().WithMessage("*ets2*");
    }

    [Fact]
    public void LoadEmbedded_Ets2_HasFullDatabaseWithBerlinCameras()
    {
        var database = CameraDatabase.LoadEmbedded(TruckGame.Ets2);

        database.Should().NotBeNull();
        database!.GameVersion.Should().Be("1.61.1.1");
        database.Cameras.Should().HaveCount(707);
        database.Cameras.Count(c => c.X is > 8500 and < 13500 && c.Z is > -11500 and < -8000).Should().BeGreaterThanOrEqualTo(20);
    }

    [Theory]
    [InlineData(TruckGame.Ats)]
    [InlineData(TruckGame.Unknown)]
    public void LoadEmbedded_GameWithoutDatabase_ReturnsNull(TruckGame game)
    {
        var database = CameraDatabase.LoadEmbedded(game);

        database.Should().BeNull();
    }
}
