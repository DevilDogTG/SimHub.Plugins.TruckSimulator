using DevDogs.TruckSimulator.Core.Radar;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Radar;

public class CameraDatabaseTests
{
    private const string Ets2Meta = """{"game":"ets2","game_version":"1.61.1.1","cameras":2}""";
    private const string AtsMeta = """{"game":"ats","game_version":"1.61.3.1","cameras":1,"traps":1}""";

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
    public void Parse_FacingColumn_ReadsFacingAndEmptyAsUnknown()
    {
        var csv = new StringReader("""
            uid,x,y,z,unit,model,sector,facing
            580b28fe81260001,10515.47,44.14,-8840.42,sign.ch_37007,speed_camera_ch,sec+0002-0003,85.8
            580b2d100ba60000,10280.59,43.83,-8798.37,sign.ch_37007,speed_camera_ch,sec+0002-0003,
            """);

        var database = CameraDatabase.Parse(TruckGame.Ets2, Ets2Meta, csv);

        database.Cameras.Select(c => c.Facing).Should().Equal(85.8, null);
    }

    [Fact]
    public void Parse_KindColumn_ReadsTrapsAndCameras()
    {
        var csv = new StringReader("""
            uid,x,y,z,unit,model,sector,facing,kind
            1,1,2,3,model.nm_474,speedcamera_pole,sec+0000+0003,90.0,camera
            2,4,5,6,model.225,police_sc,sec+0000+0008,,trap
            """);

        var database = CameraDatabase.Parse(TruckGame.Ats, AtsMeta, csv);

        database.Cameras.Select(c => c.Kind).Should().Equal(SpeedCameraKind.Camera, SpeedCameraKind.Trap);
    }

    [Fact]
    public void Parse_NoFacingColumn_LoadsWithUnknownFacing()
    {
        var csv = new StringReader("""
            uid,x,y,z,unit,model,sector
            ok,1,2,3,unit,model,sector
            """);

        var database = CameraDatabase.Parse(TruckGame.Ets2, Ets2Meta, csv);

        database.Cameras.Single().Facing.Should().BeNull();
        database.Cameras.Single().Kind.Should().Be(SpeedCameraKind.Camera);
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
        database!.GameVersion.Should().Be("1.61.1.9");
        database.Cameras.Should().HaveCount(707);
        database.Cameras.Count(c => c.X is > 8500 and < 13500 && c.Z is > -11500 and < -8000).Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void LoadEmbedded_Ets2_OnlyTwoWayCamerasHaveNoFacing()
    {
        string[] twoWay =
        [
            "r1_speedcamera_var1_ru", "rs_speed_camera_hw", "speed_camera_01_lu_lux", "speedcamera_hw_3lane_fr",
            "speedcamera_hw_dk", "speedcamera_hw_fr", "speedcamera_r1_tr",
        ];

        var database = CameraDatabase.LoadEmbedded(TruckGame.Ets2)!;

        database.Cameras.Where(c => c.Facing is null).Select(c => c.Model).Distinct().Should().BeEquivalentTo(twoWay);
        database.Cameras.Where(c => twoWay.Contains(c.Model)).Should().OnlyContain(c => c.Facing == null);
    }

    /// <summary>Facings checked against the game: fines, the owner's reports and in-game checks.</summary>
    [Theory]
    [InlineData("580b2d100ba60000", 249.2)] // Berlin speed_camera_ch: fined driving 249° (ADR-0005)
    [InlineData("580b28fe81260001", 85.8)]  // Berlin speed_camera_ch: never fined driving the other way
    [InlineData("299b948838c1000e", 266.4)] // Finland speedcamera_01_fi pair: built facing the other way (Kouvola report)
    [InlineData("299b9488db41000d", 86.4)]
    [InlineData("2cb3e398064d0003", 338.6)] // Erfurt speedcamera_hw_ch pair, back-to-back in the median (owner check)
    [InlineData("2cb3e39825cd0006", 159.6)]
    [InlineData("60c754de8c870001", 258.0)] // Belgium speed_camera_02_be pair: reverse-direction hookup
    [InlineData("60c754f2ef870000", 75.8)]
    [InlineData("01567d2460500f89", 217.7)] // Italy speedcamera_small_01_it: built side-on, hookup corrected
    [InlineData("21f217b4504d0000", 240.8)] // Italy small_01_it pair at the toll section: the truck's side (owner report)
    [InlineData("21f2174476cd0001", 59.2)]  // ... and the camera across the road
    public void LoadEmbedded_Ets2_KnownCamerasFaceTheEnforcedDirection(
        string uid,
        double facing)
    {
        var database = CameraDatabase.LoadEmbedded(TruckGame.Ets2)!;

        database.Cameras.Single(c => c.Uid == uid).Facing.Should().Be(facing);
    }

    [Fact]
    public void LoadEmbedded_Ets2_HasNoTraps()
    {
        var database = CameraDatabase.LoadEmbedded(TruckGame.Ets2)!;

        database.Cameras.Should().OnlyContain(c => c.Kind == SpeedCameraKind.Camera);
    }

    [Fact]
    public void LoadEmbedded_Ats_HasCamerasAndPoliceTraps()
    {
        var database = CameraDatabase.LoadEmbedded(TruckGame.Ats);

        database.Should().NotBeNull();
        database!.GameVersion.Should().Be("1.61.3.1");
        database.Cameras.Where(c => c.Kind == SpeedCameraKind.Camera).Should().HaveCount(5)
            .And.OnlyContain(c => c.Model == "speedcamera_pole" && c.Facing != null);
        database.Cameras.Where(c => c.Kind == SpeedCameraKind.Trap).Should().HaveCount(16)
            .And.OnlyContain(c => c.Model == "police_sc" && c.Facing == null);
    }

    [Fact]
    public void LoadEmbedded_GameWithoutDatabase_ReturnsNull()
    {
        var database = CameraDatabase.LoadEmbedded(TruckGame.Unknown);

        database.Should().BeNull();
    }
}
