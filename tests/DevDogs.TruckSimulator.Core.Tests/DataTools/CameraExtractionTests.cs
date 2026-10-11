using System.Buffers.Binary;
using System.Text;
using DevDogs.TruckSimulator.DataTools.Cameras;
using DevDogs.TruckSimulator.DataTools.Scs;

namespace DevDogs.TruckSimulator.Core.Tests.DataTools;

public class CameraExtractionTests
{
    [Theory]
    [InlineData("ch_37007")]
    [InlineData("dlc_it_318")]
    [InlineData("speedcam_20")]
    public void Token_EncodeThenDecode_RoundTrips(string text)
    {
        var decoded = ScsToken.Decode(ScsToken.Encode(text));

        decoded.Should().Be(text);
    }

    [Fact]
    public void Token_KnownValue_EncodesBase38LowestDigitFirst()
    {
        var value = ScsToken.Encode("a1");

        value.Should().Be(11UL + 2UL * 38);
    }

    [Theory]
    [InlineData("toolongtoken1")]
    [InlineData("UPPER")]
    public void Token_InvalidText_Throws(string text)
    {
        var encode = () => ScsToken.Encode(text);

        encode.Should().Throw<ArgumentException>();
    }

    /// <summary>Node quaternions and fines from real drives (2026-10-10/11, ETS2 1.61.1.1, Berlin).</summary>
    [Theory]
    [InlineData(0.7323f, -0.681f, 85.8)]     // 580b28: never fined driving the other way (261°)
    [InlineData(-0.5678f, -0.8232f, 249.2)]  // 580b2d: fined driving 249°
    [InlineData(0.9261f, 0.3772f, 315.7)]    // 580b6b12: fined driving 319°
    [InlineData(1f, 0f, 0.0)]                // no rotation: forward is -Z, north
    public void CompassFromYawQuaternion_RealNodes_GivesEnforcedTravelHeading(
        float w,
        float y,
        double expected)
    {
        var facing = MapCameras.CompassFromYawQuaternion(w, y);

        facing.Should().BeApproximately(expected, 0.1);
    }

    [Fact]
    public void NodeFacing_SkipsItemReferenceAndReadsNodeRecordAtCamera()
    {
        const ulong node = 0x580b28fef7260000;
        var sector = new List<byte>();
        sector.AddRange(NodeRecord(node, 0, 0, 0, 0f, 0f));               // the item's own reference: zeros
        sector.AddRange(new byte[13]);
        sector.AddRange(NodeRecord(node, 10515.47, 42.48, -8840.67, 0.7323f, -0.681f));

        var facing = MapCameras.NodeFacing([.. sector], node, 10515.47, -8840.42);

        facing.Should().BeApproximately(85.8, 0.1);
    }

    [Fact]
    public void NodeFacing_NoRecordNearCamera_ReturnsNull()
    {
        var sector = NodeRecord(42, 0, 0, 0, 1f, 0f);

        var facing = MapCameras.NodeFacing(sector, 42, 5000, 5000);

        facing.Should().BeNull();
    }

    [Fact]
    public void PmgLocators_ReadsHookupNames()
    {
        var pmg = Pmg(("speedcam", "speedcam.20"), ("light", null));

        var locators = PmgLocators.Read(pmg);

        locators.Should().Equal(new PmgLocator("speedcam", "speedcam.20"), new PmgLocator("light", null));
    }

    [Fact]
    public void PmgLocators_NotAPmg_Throws()
    {
        var read = () => PmgLocators.Read(new byte[200]);

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseSignUnits_FindsSignModelUnitsWithTheirModel()
    {
        const string sii = """
            SiiNunit
            {
            sign_model : sign.ch_37007
            {
             model_desc: "/model/sign/traffic/ch/speed_camera_ch.pmd"
             category: "traffic"
            }
            sign_template : sign.tmpl
            {
             model_desc: "/model/sign/navigation/board.pmd"
            }
            }
            """;

        var units = CameraModels.ParseSignUnits(sii).ToList();

        units.Should().Equal(("sign.ch_37007", "model/sign/traffic/ch/speed_camera_ch"));
    }

    [Theory]
    [InlineData(44.0, "44.0")]
    [InlineData(916.5, "916.5")]
    [InlineData(-8840.42, "-8840.42")]
    public void Number_FormatsLikePythonFloat(
        double value,
        string expected)
    {
        var text = CameraDatabaseFile.Number(value);

        text.Should().Be(expected);
    }

    [Fact]
    public void ToCsv_WritesFacingColumnAndEmptyWhenUnknown()
    {
        MapCamera[] cameras =
        [
            new(0x580b28fe81260001, 10515.47, 44.14, -8840.42, "sign.ch_37007", "speed_camera_ch", "sec+0002-0003", 85.8),
            new(0x1, 1, 2, 3, "sign.x", "cam", "sec+0000+0000", null),
        ];

        var csv = CameraDatabaseFile.ToCsv(cameras);

        csv.Should().Be(
            "uid,x,y,z,unit,model,sector,facing\n"
            + "580b28fe81260001,10515.47,44.14,-8840.42,sign.ch_37007,speed_camera_ch,sec+0002-0003,85.8\n"
            + "0000000000000001,1.0,2.0,3.0,sign.x,cam,sec+0000+0000,\n");
    }

    private static byte[] NodeRecord(
        ulong uid,
        double x,
        double y,
        double z,
        float w,
        float qy)
    {
        var record = new byte[36];
        BinaryPrimitives.WriteUInt64LittleEndian(record, uid);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(8), (int)Math.Round(x * 256));
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(12), (int)Math.Round(y * 256));
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(16), (int)Math.Round(z * 256));
        BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(20), w);
        BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(28), qy);
        return record;
    }

    /// <summary>A minimal PMG 0x15: header, locator table, string pool.</summary>
    private static byte[] Pmg(params (string Name, string? Hookup)[] locators)
    {
        const int header = 112;
        var strings = new MemoryStream();
        var table = new byte[locators.Length * 44];
        for (var i = 0; i < locators.Length; i++)
        {
            var entry = table.AsSpan(i * 44);
            BinaryPrimitives.WriteUInt64LittleEndian(entry, ScsToken.Encode(locators[i].Name));
            var hookupAt = -1;
            if (locators[i].Hookup is { } hookup)
            {
                hookupAt = (int)strings.Length;
                strings.Write(Encoding.ASCII.GetBytes(hookup + "\0"));
            }

            BinaryPrimitives.WriteInt32LittleEndian(entry[40..], hookupAt);
        }

        var pmg = new byte[header + table.Length + strings.Length];
        pmg[0] = 0x15;
        "gmP"u8.CopyTo(pmg.AsSpan(1));
        BinaryPrimitives.WriteInt32LittleEndian(pmg.AsSpan(20), locators.Length);
        BinaryPrimitives.WriteInt32LittleEndian(pmg.AsSpan(80), header);
        BinaryPrimitives.WriteInt32LittleEndian(pmg.AsSpan(88), header + table.Length);
        table.CopyTo(pmg.AsSpan(header));
        strings.ToArray().CopyTo(pmg.AsSpan(header + table.Length));
        return pmg;
    }
}
