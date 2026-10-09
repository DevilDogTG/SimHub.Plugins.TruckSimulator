using System;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Light states the game does not report directly.
/// </summary>
public sealed class LightsSection : ITelemetrySection
{
    /// <summary>Property: the hazard warning lights are on.</summary>
    public const string HazardWarningOn = "Lights.HazardWarningOn";

    /// <summary>
    /// How long the hazard state is held after both blinkers were last seen on. Blinkers flash, and
    /// the hold keeps the property steady between flashes.
    /// </summary>
    private static readonly TimeSpan _hold = TimeSpan.FromSeconds(1);

    private bool _hazardOn;
    private DateTime _bothLastOnAt = DateTime.MinValue;

    /// <inheritdoc />
    public void Register(ISectionHost host) => host.AddProperty(HazardWarningOn, false);

    /// <summary>
    /// Treats both blinkers on at once as hazard lights, since a driver can't indicate both ways.
    /// </summary>
    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var bothOn = telemetry.Truck.BlinkerLeftOn && telemetry.Truck.BlinkerRightOn;

        if (bothOn)
        {
            _bothLastOnAt = now;
            _hazardOn = true;
        }
        else if (now > _bothLastOnAt + _hold)
        {
            _hazardOn = false;
        }

        output.SetProperty(HazardWarningOn, _hazardOn);
    }
}
