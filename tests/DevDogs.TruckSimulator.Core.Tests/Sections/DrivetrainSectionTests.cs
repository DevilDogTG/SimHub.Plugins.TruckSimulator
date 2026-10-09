using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class DrivetrainSectionTests
{
    private static TruckTelemetry Truck(TruckState truck) => new() { Truck = truck };

    private static FakeSectionHost Run(TruckState truck)
    {
        var host = new FakeSectionHost();
        new DrivetrainSection().Update(Truck(truck), DateTime.UnixEpoch, host);
        return host;
    }

    [Theory]
    [InlineData("vehicle.mercedes.actros2014", 1100d, true, 1000, 1200)]
    [InlineData("vehicle.mercedes.actros2014", 1250d, false, 1000, 1200)]
    [InlineData("vehicle.renault.t", 1350d, true, 1000, 1400)]
    [InlineData("vehicle.unknown.truck", 1350d, false, 1000, 1300)]
    [InlineData("", 1300d, true, 1000, 1300)]
    public void Update_TruckAndRpm_PublishesPeakTorqueBand(
        string truckId,
        double rpm,
        bool expectedInBand,
        int expectedMin,
        int expectedMax)
    {
        var host = Run(new TruckState { Id = truckId, EngineRpm = rpm });

        host.Properties.Should().Contain(DrivetrainSection.PeakTorque, expectedInBand);
        host.Properties.Should().Contain(DrivetrainSection.PeakTorqueMin, expectedMin);
        host.Properties.Should().Contain(DrivetrainSection.PeakTorqueMax, expectedMax);
    }

    [Fact]
    public void Update_FuelRangeDropsToZero_HoldsLastRange()
    {
        var host = new FakeSectionHost();
        var section = new DrivetrainSection();
        section.Update(Truck(new TruckState { FuelRange = 640f }), DateTime.UnixEpoch, host);

        section.Update(Truck(new TruckState { FuelRange = 0f }), DateTime.UnixEpoch, host);

        host.Properties.Should().Contain(DrivetrainSection.FuelRangeStable, 640f);
    }

    [Fact]
    public void Update_AverageConsumption_ConvertsToImperialUnits()
    {
        var host = Run(new TruckState { FuelAverageConsumption = 0.35f });

        ((float)host.Properties[DrivetrainSection.LitresPer100Mile]).Should().BeApproximately(56.327f, 0.01f);
        ((float)host.Properties[DrivetrainSection.MilesPerGallonUk]).Should().BeApproximately(8.071f, 0.01f);
        ((float)host.Properties[DrivetrainSection.MilesPerGallonUs]).Should().BeApproximately(6.720f, 0.01f);
    }

    [Fact]
    public void Update_ConsumptionDropsToZero_HoldsLastConversions()
    {
        var host = new FakeSectionHost();
        var section = new DrivetrainSection();
        section.Update(Truck(new TruckState { FuelAverageConsumption = 0.35f }), DateTime.UnixEpoch, host);

        section.Update(Truck(new TruckState { FuelAverageConsumption = 0f }), DateTime.UnixEpoch, host);

        ((float)host.Properties[DrivetrainSection.MilesPerGallonUk]).Should().BeApproximately(8.071f, 0.01f);
    }

    [Fact]
    public void Update_NoConsumptionYet_PublishesZeroEconomy()
    {
        var host = Run(new TruckState());

        host.Properties.Should().Contain(DrivetrainSection.MilesPerGallonUk, 0f);
        host.Properties.Should().Contain(DrivetrainSection.MilesPerGallonUs, 0f);
    }

    [Theory]
    [InlineData(0, 12, "N")]
    [InlineData(-1, 12, "R1")]
    [InlineData(-2, 14, "R2")]
    [InlineData(5, 12, "5")]
    [InlineData(1, 14, "C1")]
    [InlineData(2, 14, "C2")]
    [InlineData(3, 14, "1")]
    [InlineData(14, 14, "12")]
    public void GearLabel_GearAndBox_ReturnsDashboardLabel(
        int gear,
        int forwardGears,
        string expected)
    {
        var label = DrivetrainSection.GearLabel(gear, forwardGears);

        label.Should().Be(expected);
    }
}
