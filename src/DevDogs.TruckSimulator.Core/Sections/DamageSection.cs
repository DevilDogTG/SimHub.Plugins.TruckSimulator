using System;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Overall truck wear and a warning threshold.
/// </summary>
/// <param name="settings">Supplies the warning threshold.</param>
public sealed class DamageSection(PluginSettings settings) : ITelemetrySection
{
    /// <summary>Property: average wear is above the user's warning level.</summary>
    public const string WearWarning = "Damage.WearWarning";

    /// <summary>Property: average wear of cabin, chassis, engine, transmission and wheels, in percent (0-100).</summary>
    public const string WearAverage = "Damage.WearAverage";

    /// <summary>Event: average wear rose by more than one percentage point since the previous update.</summary>
    public const string DamageIncrease = "DamageIncrease";

    /// <summary>The rise in average wear, in percentage points, that counts as taking damage.</summary>
    private const float IncreaseThreshold = 1f;

    private float? _previousAverage;

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(WearWarning, false);
        host.AddProperty(WearAverage, 0f);
        host.AddEvent(DamageIncrease);
    }

    /// <summary>
    /// The first update only records a baseline, so an already damaged truck does not fire
    /// <see cref="DamageIncrease"/> when the game starts.
    /// </summary>
    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var average = AverageWearPercent(telemetry.Truck.Damage);

        if (average > _previousAverage + IncreaseThreshold)
        {
            output.TriggerEvent(DamageIncrease);
        }

        _previousAverage = average;

        output.SetProperty(WearWarning, average > settings.WearWarningLevel);
        output.SetProperty(WearAverage, average);
    }

    /// <summary>
    /// Averages the wear of the truck's parts.
    /// </summary>
    /// <param name="damage">The wear of each part, 0..1.</param>
    /// <returns>The average wear in percent.</returns>
    private static float AverageWearPercent(TruckDamage damage) =>
        (damage.Cabin + damage.Chassis + damage.Engine + damage.Transmission + damage.WheelsAverage) / 5f * 100f;
}
