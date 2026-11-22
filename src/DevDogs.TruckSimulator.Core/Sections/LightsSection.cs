using System;
using DevDogs.TruckSimulator.Core.GameConfig;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Light states the game does not report directly.
/// </summary>
/// <param name="settings">Supplies the "Automatic high beams" setting.</param>
/// <param name="autoHighBeamFromProfile">
/// Returns the game's "Automatic high beams" option as read from the loaded profile's <c>config.cfg</c>,
/// or <see langword="null"/> when unknown.
/// </param>
public sealed class LightsSection(
    PluginSettings settings,
    Func<bool?> autoHighBeamFromProfile) : ITelemetrySection
{
    /// <summary>Property: the hazard warning lights are on.</summary>
    public const string HazardWarningOn = "Lights.HazardWarningOn";

    /// <summary>
    /// Property: the game's "Automatic high beams" option is on, so the game may dim the high beams
    /// while the switch stays on. Unknown counts as off.
    /// </summary>
    public const string AutoHighBeam = "Lights.AutoHighBeam";

    /// <summary>
    /// How long the hazard state is held after both blinkers were last seen on. Blinkers flash, and
    /// the hold keeps the property steady between flashes.
    /// </summary>
    private static readonly TimeSpan _hold = TimeSpan.FromSeconds(1);

    private bool _hazardOn;
    private DateTime _bothLastOnAt = DateTime.MinValue;

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(HazardWarningOn, false);
        host.AddProperty(AutoHighBeam, false);
    }

    /// <summary>
    /// Treats both blinkers on at once as hazard lights, since a driver can't indicate both ways, and
    /// publishes the automatic high beam option.
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
        output.SetProperty(AutoHighBeam, AutoHighBeamSetting.Resolve(settings.AutoHighBeam, autoHighBeamFromProfile()));
    }
}
