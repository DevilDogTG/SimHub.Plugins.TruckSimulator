using System;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// The route advisor's estimated time left, split into parts for display.
/// </summary>
public sealed class NavigationSection : ITelemetrySection
{
    /// <summary>Property: whole days left on the route.</summary>
    public const string TotalDaysLeft = "Navigation.TotalDaysLeft";

    /// <summary>
    /// Property: the hours part (0-23) of the time left. Named "Total" by the original plugin; kept
    /// for dashboard compatibility.
    /// </summary>
    public const string TotalHoursLeft = "Navigation.TotalHoursLeft";

    /// <summary>Property: the minutes part (0-59) of the time left.</summary>
    public const string Minutes = "Navigation.Minutes";

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        host.AddProperty(TotalDaysLeft, 0);
        host.AddProperty(TotalHoursLeft, 0);
        host.AddProperty(Minutes, 0);
    }

    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var time = telemetry.Navigation.Time;

        output.SetProperty(TotalDaysLeft, time.Days);
        output.SetProperty(TotalHoursLeft, time.Hours);
        output.SetProperty(Minutes, time.Minutes);
    }
}
