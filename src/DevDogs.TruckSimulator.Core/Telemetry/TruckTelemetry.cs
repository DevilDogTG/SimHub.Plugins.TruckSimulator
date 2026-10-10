using System;

namespace DevDogs.TruckSimulator.Core.Telemetry;

/// <summary>
/// An immutable snapshot of the game values the calculators need for one update tick.
/// Built by the plugin from SimHub's raw telemetry; every value has a safe default when the game
/// does not provide it.
/// </summary>
public sealed record TruckTelemetry
{
    /// <summary>Gets the game the snapshot came from.</summary>
    public TruckGame Game { get; init; }

    /// <summary>Gets the truck's state.</summary>
    public TruckState Truck { get; init; } = new();

    /// <summary>Gets the current job.</summary>
    public JobState Job { get; init; } = new();

    /// <summary>Gets the route advisor's navigation values.</summary>
    public NavigationState Navigation { get; init; } = new();

    /// <summary>Gets the in-game time left until the driver must rest.</summary>
    public TimeSpan NextRestStop { get; init; }

    /// <summary>Gets the fine state: whether one was just given, and the last fine's details.</summary>
    public FineState Fine { get; init; } = new();
}

/// <summary>
/// Fines the game gives the driver. <see cref="Active"/> is raised when a fine is given, but stays
/// raised afterwards (seen for 6.5 minutes, through a disconnect, on 2026-10-10), so it marks only the
/// first fine reliably; a change in <see cref="Offence"/> or <see cref="Amount"/> also signals a new
/// fine, though two identical fines in a row can't be told apart.
/// </summary>
public sealed record FineState
{
    /// <summary>Gets a value indicating whether the game reports that a fine was given.</summary>
    public bool Active { get; init; }

    /// <summary>Gets the last fine's offence as the game names it, for example <c>Speeding_camera</c> or <c>Speeding</c>; empty before the first fine.</summary>
    public string Offence { get; init; } = "";

    /// <summary>Gets the last fine's amount in the game's currency; 0 before the first fine.</summary>
    public long Amount { get; init; }
}

/// <summary>
/// The truck's identity, drivetrain, fuel, lights and damage.
/// </summary>
public sealed record TruckState
{
    /// <summary>Gets the game's truck id, for example <c>vehicle.scania.r_2016</c>; empty when unknown.</summary>
    public string Id { get; init; } = "";

    /// <summary>Gets the engine speed in revolutions per minute.</summary>
    public double EngineRpm { get; init; }

    /// <summary>Gets a value indicating whether the engine is running.</summary>
    public bool EngineEnabled { get; init; }

    /// <summary>Gets the gear shown on the dashboard: negative for reverse, 0 for neutral.</summary>
    public int GearDashboard { get; init; }

    /// <summary>Gets the number of forward gears of the transmission.</summary>
    public int ForwardGearCount { get; init; }

    /// <summary>Gets the truck's speed in miles per hour.</summary>
    public float SpeedMph { get; init; }

    /// <summary>Gets the average fuel consumption in litres per kilometre; 0 when the game has no value yet.</summary>
    public float FuelAverageConsumption { get; init; }

    /// <summary>Gets the estimated fuel range in kilometres; 0 when the game has no value yet.</summary>
    public float FuelRange { get; init; }

    /// <summary>Gets the truck's position in the game world, in metres.</summary>
    public WorldPosition Position { get; init; } = new();

    /// <summary>
    /// Gets the truck's heading as a fraction of a full turn (0..1), as the SCS SDK reports it.
    /// </summary>
    public float Heading { get; init; }

    /// <summary>Gets a value indicating whether the left blinker is on.</summary>
    public bool BlinkerLeftOn { get; init; }

    /// <summary>Gets a value indicating whether the right blinker is on.</summary>
    public bool BlinkerRightOn { get; init; }

    /// <summary>Gets the wear of the truck's parts.</summary>
    public TruckDamage Damage { get; init; } = new();
}

/// <summary>
/// A point in the game world, in metres. X and Z are the ground plane; Y is height.
/// </summary>
public sealed record WorldPosition
{
    /// <summary>Gets the X coordinate.</summary>
    public double X { get; init; }

    /// <summary>Gets the Y coordinate (height).</summary>
    public double Y { get; init; }

    /// <summary>Gets the Z coordinate.</summary>
    public double Z { get; init; }
}

/// <summary>
/// Wear of the truck's parts, each from 0 (new) to 1 (fully worn).
/// </summary>
public sealed record TruckDamage
{
    /// <summary>Gets the cabin wear.</summary>
    public float Cabin { get; init; }

    /// <summary>Gets the chassis wear.</summary>
    public float Chassis { get; init; }

    /// <summary>Gets the engine wear.</summary>
    public float Engine { get; init; }

    /// <summary>Gets the transmission wear.</summary>
    public float Transmission { get; init; }

    /// <summary>Gets the average wear of the wheels.</summary>
    public float WheelsAverage { get; init; }
}

/// <summary>
/// The job the driver currently has. All ids are empty when there is no job.
/// </summary>
public sealed record JobState
{
    /// <summary>Gets the cargo id.</summary>
    public string CargoId { get; init; } = "";

    /// <summary>Gets the source company id.</summary>
    public string CompanySourceId { get; init; } = "";

    /// <summary>Gets the source city id.</summary>
    public string CitySourceId { get; init; } = "";

    /// <summary>Gets the source city name as the game reports it.</summary>
    public string CitySource { get; init; } = "";

    /// <summary>Gets the destination company id.</summary>
    public string CompanyDestinationId { get; init; } = "";

    /// <summary>Gets the destination city id.</summary>
    public string CityDestinationId { get; init; } = "";

    /// <summary>Gets the destination city name as the game reports it.</summary>
    public string CityDestination { get; init; } = "";

    /// <summary>Gets the in-game time left to deliver.</summary>
    public TimeSpan RemainingDeliveryTime { get; init; }

    /// <summary>Gets a value indicating whether the game reports that a job is active.</summary>
    public bool OnJob { get; init; }
}

/// <summary>
/// Route advisor values.
/// </summary>
public sealed record NavigationState
{
    /// <summary>Gets the distance left on the planned route, in metres; 0 when there is no route.</summary>
    public float Distance { get; init; }

    /// <summary>Gets the estimated in-game time left on the planned route.</summary>
    public TimeSpan Time { get; init; }

    /// <summary>Gets the current road's speed limit in miles per hour; 0 when there is none.</summary>
    public float SpeedLimitMph { get; init; }
}
