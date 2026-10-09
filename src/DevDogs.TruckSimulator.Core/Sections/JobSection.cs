using System;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Speeding, rest and delivery-time values for the current drive.
/// </summary>
/// <param name="settings">Supplies the over-speed margin.</param>
public sealed class JobSection(PluginSettings settings) : ITelemetrySection
{
    /// <summary>Property: speed is above the limit plus the user's margin.</summary>
    public const string OverSpeedLimit = "Job.OverSpeedLimit";

    /// <summary>Property: 0..1 position of the speed between the limit and the limit plus margin.</summary>
    public const string OverSpeedLimitPercentage = "Job.OverSpeedLimitPercentage";

    /// <summary>Property: less than one in-game hour until the driver must rest.</summary>
    public const string NextRestWarning = "Job.NextRestWarning";

    /// <summary>Property: the days part of the delivery time left.</summary>
    public const string RemainingDays = "Job.RemainingDeliveryTime.Time.Days";

    /// <summary>Property: the hours part (0-23) of the delivery time left.</summary>
    public const string RemainingHours = "Job.RemainingDeliveryTime.Time.Hours";

    /// <summary>Property: the minutes part (0-59) of the delivery time left.</summary>
    public const string RemainingMinutes = "Job.RemainingDeliveryTime.Time.Minutes";

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(OverSpeedLimit, false);
        host.AddProperty(OverSpeedLimitPercentage, 0f);
        host.AddProperty(NextRestWarning, false);
        host.AddProperty(RemainingDays, 0);
        host.AddProperty(RemainingHours, 0);
        host.AddProperty(RemainingMinutes, 0);
    }

    /// <summary>
    /// Speeding is only evaluated on roads with a known speed limit.
    /// </summary>
    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var limit = telemetry.Navigation.SpeedLimitMph;
        var speed = telemetry.Truck.SpeedMph;
        var threshold = limit + settings.OverSpeedMargin;
        var hasLimit = limit > 0;

        output.SetProperty(OverSpeedLimit, hasLimit && speed > threshold);
        output.SetProperty(
            OverSpeedLimitPercentage,
            hasLimit ? RangeMath.FractionOfRange(speed, Math.Min(limit, threshold), Math.Max(limit, threshold)) : 0f);

        output.SetProperty(NextRestWarning, telemetry.NextRestStop.TotalHours < 1);

        var remaining = telemetry.Job.RemainingDeliveryTime;
        output.SetProperty(RemainingDays, remaining.Days);
        output.SetProperty(RemainingHours, remaining.Hours);
        output.SetProperty(RemainingMinutes, remaining.Minutes);
    }
}
