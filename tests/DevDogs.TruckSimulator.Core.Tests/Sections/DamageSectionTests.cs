using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class DamageSectionTests
{
    /// <summary>Every part worn by the same fraction, so the average equals it.</summary>
    private static TruckTelemetry UniformWear(float wear) => new()
    {
        Truck = new TruckState
        {
            Damage = new TruckDamage { Cabin = wear, Chassis = wear, Engine = wear, Transmission = wear, WheelsAverage = wear },
        },
    };

    [Fact]
    public void Update_UniformWear_PublishesAverageInPercent()
    {
        var host = new FakeSectionHost();

        new DamageSection(new PluginSettings()).Update(UniformWear(0.04f), DateTime.UnixEpoch, host);

        ((float)host.Properties[DamageSection.WearAverage]).Should().BeApproximately(4f, 0.001f);
    }

    [Fact]
    public void Update_MixedWear_AveragesAllFiveParts()
    {
        var host = new FakeSectionHost();
        var telemetry = new TruckTelemetry
        {
            Truck = new TruckState { Damage = new TruckDamage { Cabin = 0.5f, Engine = 0.25f, WheelsAverage = 0.25f } },
        };

        new DamageSection(new PluginSettings()).Update(telemetry, DateTime.UnixEpoch, host);

        ((float)host.Properties[DamageSection.WearAverage]).Should().BeApproximately(20f, 0.001f);
    }

    [Theory]
    [InlineData(0.04f, false)]
    [InlineData(0.05f, false)]
    [InlineData(0.06f, true)]
    public void Update_AverageAgainstWarningLevel_PublishesWarningAboveLevel(
        float wear,
        bool expected)
    {
        var host = new FakeSectionHost();

        new DamageSection(new PluginSettings { WearWarningLevel = 5 }).Update(UniformWear(wear), DateTime.UnixEpoch, host);

        host.Properties.Should().Contain(DamageSection.WearWarning, expected);
    }

    [Fact]
    public void Update_FirstTickWithExistingDamage_DoesNotTriggerIncrease()
    {
        var host = new FakeSectionHost();

        new DamageSection(new PluginSettings()).Update(UniformWear(0.3f), DateTime.UnixEpoch, host);

        host.TriggeredEvents.Should().BeEmpty();
    }

    [Fact]
    public void Update_RiseAboveOnePoint_TriggersIncrease()
    {
        var host = new FakeSectionHost();
        var section = new DamageSection(new PluginSettings());
        section.Update(UniformWear(0.10f), DateTime.UnixEpoch, host);

        section.Update(UniformWear(0.115f), DateTime.UnixEpoch, host);

        host.TriggeredEvents.Should().Equal(DamageSection.DamageIncrease);
    }

    [Theory]
    [InlineData(0.105f)]
    [InlineData(0.05f)]
    public void Update_SmallRiseOrRepair_DoesNotTriggerIncrease(float nextWear)
    {
        var host = new FakeSectionHost();
        var section = new DamageSection(new PluginSettings());
        section.Update(UniformWear(0.10f), DateTime.UnixEpoch, host);

        section.Update(UniformWear(nextWear), DateTime.UnixEpoch, host);

        host.TriggeredEvents.Should().BeEmpty();
    }
}
