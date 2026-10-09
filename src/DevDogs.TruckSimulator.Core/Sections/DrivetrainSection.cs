using System;
using System.Collections.Generic;
using System.Globalization;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Peak-torque band, steadied fuel values and a dashboard gear label.
/// </summary>
public sealed class DrivetrainSection : ITelemetrySection
{
    /// <summary>Property: engine speed is inside the truck's peak-torque band.</summary>
    public const string PeakTorque = "Drivetrain.PeakTorque";

    /// <summary>Property: lower end of the peak-torque band, in RPM.</summary>
    public const string PeakTorqueMin = "Drivetrain.PeakTorque.Min";

    /// <summary>Property: upper end of the peak-torque band, in RPM.</summary>
    public const string PeakTorqueMax = "Drivetrain.PeakTorque.Max";

    /// <summary>Property: fuel range in km, holding the last value while the game briefly reports 0.</summary>
    public const string FuelRangeStable = "Drivetrain.FuelRangeStable";

    /// <summary>Property: average consumption in litres per 100 miles.</summary>
    public const string LitresPer100Mile = "Drivetrain.FuelValue.AverageConsumptionLitresPer100Mile";

    /// <summary>Property: average consumption in miles per UK gallon.</summary>
    public const string MilesPerGallonUk = "Drivetrain.FuelValue.AverageConsumptionMilesPerGallonUK";

    /// <summary>Property: average consumption in miles per US gallon.</summary>
    public const string MilesPerGallonUs = "Drivetrain.FuelValue.AverageConsumptionMilesPerGallonUS";

    /// <summary>Property: dashboard gear label: <c>N</c>, <c>R1</c>, <c>C1</c>/<c>C2</c> crawler gears on 12+2 boxes, or the gear number.</summary>
    public const string GearDashboard = "Drivetrain.GearDashboard";

    private const float KilometresPerMile = 1.609344f;

    /// <summary>Miles per UK gallon for a consumption of 1 litre per km (4.54609 L/gal ÷ 1.609344 km/mi).</summary>
    private const float MpgUkAtOneLitrePerKm = 2.824809363f;

    /// <summary>Miles per US gallon for a consumption of 1 litre per km (3.785411784 L/gal ÷ 1.609344 km/mi).</summary>
    private const float MpgUsAtOneLitrePerKm = 2.352145833f;

    /// <summary>A 12+2 transmission: 12 road gears plus 2 crawler gears below them.</summary>
    private const int CrawlerTransmissionForwardGears = 14;

    private static readonly (int Min, int Max) _defaultPeakTorque = (1000, 1300);

    /// <summary>
    /// Peak-torque bands of base trucks that differ from the game's default. The game does not report
    /// torque curves, so these come from each base truck's published figures and ignore engine upgrades.
    /// </summary>
    private static readonly Dictionary<string, (int Min, int Max)> _peakTorqueByTruck = new(StringComparer.Ordinal)
    {
        ["vehicle.man.tgx_euro6"] = (1000, 1400),
        ["vehicle.mercedes.actros"] = (1000, 1400),
        ["vehicle.mercedes.actros2014"] = (1000, 1200),
        ["vehicle.renault.t"] = (1000, 1400),
        ["vehicle.scania.r_2016"] = (1000, 1300),
    };

    private float _stableFuelRange;
    private float _stableAverageConsumption;

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(PeakTorque, false);
        host.AddProperty(PeakTorqueMin, _defaultPeakTorque.Min);
        host.AddProperty(PeakTorqueMax, _defaultPeakTorque.Max);
        host.AddProperty(FuelRangeStable, 0f);
        host.AddProperty(LitresPer100Mile, 0f);
        host.AddProperty(MilesPerGallonUk, 0f);
        host.AddProperty(MilesPerGallonUs, 0f);
        host.AddProperty(GearDashboard, "N");
    }

    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var truck = telemetry.Truck;

        var band = _peakTorqueByTruck.TryGetValue(truck.Id, out var known) ? known : _defaultPeakTorque;
        output.SetProperty(PeakTorque, truck.EngineRpm >= band.Min && truck.EngineRpm <= band.Max);
        output.SetProperty(PeakTorqueMin, band.Min);
        output.SetProperty(PeakTorqueMax, band.Max);

        _stableFuelRange = truck.FuelRange > 0 ? truck.FuelRange : _stableFuelRange;
        _stableAverageConsumption = truck.FuelAverageConsumption > 0 ? truck.FuelAverageConsumption : _stableAverageConsumption;

        output.SetProperty(FuelRangeStable, _stableFuelRange);
        output.SetProperty(LitresPer100Mile, _stableAverageConsumption * 100f * KilometresPerMile);
        output.SetProperty(MilesPerGallonUk, MilesPerGallon(_stableAverageConsumption, MpgUkAtOneLitrePerKm));
        output.SetProperty(MilesPerGallonUs, MilesPerGallon(_stableAverageConsumption, MpgUsAtOneLitrePerKm));

        output.SetProperty(GearDashboard, GearLabel(truck.GearDashboard, truck.ForwardGearCount));
    }

    /// <summary>
    /// Converts consumption to fuel economy. Economy is inversely proportional to consumption.
    /// </summary>
    /// <param name="litresPerKm">Average consumption in litres per km.</param>
    /// <param name="mpgAtOneLitrePerKm">The economy at exactly 1 L/km for the gallon in use.</param>
    /// <returns>Miles per gallon, or 0 when consumption is unknown.</returns>
    private static float MilesPerGallon(
        float litresPerKm,
        float mpgAtOneLitrePerKm) => litresPerKm > 0 ? mpgAtOneLitrePerKm / litresPerKm : 0f;

    /// <summary>
    /// Builds the dashboard gear label. The game reports crawler gears as gears 1 and 2 of a 14-speed
    /// box, so on those boxes they become <c>C1</c>/<c>C2</c> and road gears are renumbered from 1.
    /// This is inferred from the gear count; the game doesn't report crawler gears directly.
    /// </summary>
    /// <param name="gear">The dashboard gear: negative for reverse, 0 for neutral.</param>
    /// <param name="forwardGearCount">The number of forward gears.</param>
    /// <returns>The label.</returns>
    internal static string GearLabel(
        int gear,
        int forwardGearCount)
    {
        if (gear < 0)
        {
            return "R" + (-gear).ToString(CultureInfo.InvariantCulture);
        }

        if (gear == 0)
        {
            return "N";
        }

        if (forwardGearCount == CrawlerTransmissionForwardGears)
        {
            return gear <= 2
                ? "C" + gear.ToString(CultureInfo.InvariantCulture)
                : (gear - 2).ToString(CultureInfo.InvariantCulture);
        }

        return gear.ToString(CultureInfo.InvariantCulture);
    }
}
