using System;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Engine state derived from telemetry.
/// </summary>
public sealed class EngineSection : ITelemetrySection
{
    /// <summary>Property: the engine is turning but not yet running (cranking).</summary>
    public const string Starting = "Engine.Starting";

    /// <inheritdoc />
    public void Register(ISectionHost host) => host.AddProperty(Starting, false);

    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output) =>
        output.SetProperty(Starting, !telemetry.Truck.EngineEnabled && telemetry.Truck.EngineRpm > 0);
}
