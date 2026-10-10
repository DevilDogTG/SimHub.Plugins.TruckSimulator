using System;
using System.Collections.Generic;
using DevDogs.TruckSimulator.Core.Telemetry;
using ETS2Reader;
using GameReaderCommon;

namespace DevDogs.TruckSimulator.Telemetry;

/// <summary>
/// Builds a <see cref="TruckTelemetry"/> snapshot from SimHub's game data.
/// Reads SimHub's typed ETS2/ATS telemetry object directly instead of looking properties up by
/// string, so a renamed field fails the build rather than an update tick. Each value is read from
/// the same source the original plugin used.
/// </summary>
internal static class TelemetryReader
{
    /// <summary>
    /// Gets the SimHub property each snapshot field is read from, keyed by the field's path as
    /// <c>SnapshotFields</c> lists it. Shown on the live telemetry tab so a value can be compared
    /// with SimHub's own. Keep in step with <see cref="Map"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Sources { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Game"] = "DataCorePlugin.GameName",
        ["Truck.Id"] = "GameRawData.TruckValues.ConstantsValues.Id",
        ["Truck.EngineRpm"] = "DataCorePlugin.GameData.Rpms",
        ["Truck.EngineEnabled"] = "GameRawData.Drivetrain.EngineEnabled",
        ["Truck.GearDashboard"] = "GameRawData.TruckValues.CurrentValues.DashboardValues.GearDashboards",
        ["Truck.ForwardGearCount"] = "GameRawData.TruckValues.ConstantsValues.MotorValues.ForwardGearCount",
        ["Truck.SpeedMph"] = "GameRawData.Drivetrain.SpeedMph",
        ["Truck.FuelAverageConsumption"] = "GameRawData.TruckValues.CurrentValues.DashboardValues.FuelValue.AverageConsumption",
        ["Truck.FuelRange"] = "GameRawData.TruckValues.CurrentValues.DashboardValues.FuelValue.Range",
        ["Truck.Position.X"] = "GameRawData.TruckValues.CurrentValues.PositionValue.Position.X",
        ["Truck.Position.Y"] = "GameRawData.TruckValues.CurrentValues.PositionValue.Position.Y",
        ["Truck.Position.Z"] = "GameRawData.TruckValues.CurrentValues.PositionValue.Position.Z",
        ["Truck.Heading"] = "GameRawData.TruckValues.CurrentValues.PositionValue.Orientation.Heading",
        ["Truck.BlinkerLeftOn"] = "GameRawData.TruckValues.CurrentValues.LightsValues.BlinkerLeftOn",
        ["Truck.BlinkerRightOn"] = "GameRawData.TruckValues.CurrentValues.LightsValues.BlinkerRightOn",
        ["Truck.Damage.Cabin"] = "GameRawData.TruckValues.CurrentValues.DamageValues.Cabin",
        ["Truck.Damage.Chassis"] = "GameRawData.TruckValues.CurrentValues.DamageValues.Chassis",
        ["Truck.Damage.Engine"] = "GameRawData.TruckValues.CurrentValues.DamageValues.Engine",
        ["Truck.Damage.Transmission"] = "GameRawData.TruckValues.CurrentValues.DamageValues.Transmission",
        ["Truck.Damage.WheelsAverage"] = "GameRawData.TruckValues.CurrentValues.DamageValues.WheelsAvg",
        ["Job.CargoId"] = "GameRawData.JobValues.CargoValues.Id",
        ["Job.CompanySourceId"] = "GameRawData.JobValues.CompanySourceId",
        ["Job.CitySourceId"] = "GameRawData.JobValues.CitySourceId",
        ["Job.CitySource"] = "GameRawData.JobValues.CitySource",
        ["Job.CompanyDestinationId"] = "GameRawData.JobValues.CompanyDestinationId",
        ["Job.CityDestinationId"] = "GameRawData.JobValues.CityDestinationId",
        ["Job.CityDestination"] = "GameRawData.JobValues.CityDestination",
        ["Job.RemainingDeliveryTime"] = "GameRawData.JobValues.RemainingDeliveryTime.Time",
        ["Job.OnJob"] = "GameRawData.SpecialEventsValues.OnJob",
        ["Navigation.Distance"] = "GameRawData.NavigationValues.NavigationDistance",
        ["Navigation.Time"] = "GameRawData.NavigationValues.NavigationTime",
        ["Navigation.SpeedLimitMph"] = "GameRawData.Job.SpeedLimitMph",
        ["NextRestStop"] = "GameRawData.CommonValues.NextRestStop.Time",
        ["Fine.Active"] = "GameRawData.SpecialEventsValues.Fined",
        ["Fine.Offence"] = "GameRawData.GamePlay.FinedEvent.Offence",
        ["Fine.Amount"] = "GameRawData.GamePlay.FinedEvent.Amount",
    };

    /// <summary>
    /// Reads the current snapshot when a supported game is running and has data.
    /// </summary>
    /// <param name="data">SimHub's game data for this tick.</param>
    /// <param name="telemetry">The snapshot, when one could be read.</param>
    /// <returns><see langword="true"/> when <paramref name="telemetry"/> was read.</returns>
    public static bool TryRead(
        ref GameData data,
        out TruckTelemetry? telemetry)
    {
        telemetry = null;

        var game = TruckGames.FromSimHubName(data.GameName);
        if (!data.GameRunning || game == TruckGame.Unknown || data.NewData is null)
        {
            return false;
        }

        if (data.NewData.GetRawDataObject() is not TelemetryEx raw || raw.Telemetry is null)
        {
            return false;
        }

        telemetry = Map(game, data.NewData.Rpms, raw);
        return true;
    }

    /// <summary>
    /// Maps SimHub's raw telemetry to a snapshot, substituting defaults for missing values.
    /// </summary>
    /// <param name="game">The running game.</param>
    /// <param name="rpm">The engine speed SimHub normalised for this tick.</param>
    /// <param name="raw">The raw telemetry object.</param>
    /// <returns>The snapshot.</returns>
    private static TruckTelemetry Map(
        TruckGame game,
        double rpm,
        TelemetryEx raw)
    {
        var sdk = raw.Telemetry;
        var legacy = raw.Legacy;
        var constants = sdk.TruckValues?.ConstantsValues;
        var current = sdk.TruckValues?.CurrentValues;
        var dashboard = current?.DashboardValues;
        var lights = current?.LightsValues;
        var placement = current?.PositionValue;
        var damage = current?.DamageValues;
        var job = sdk.JobValues;
        var navigation = sdk.NavigationValues;

        return new TruckTelemetry
        {
            Game = game,
            NextRestStop = sdk.CommonValues?.NextRestStop?.Time ?? TimeSpan.Zero,
            Fine = new FineState
            {
                Active = sdk.SpecialEventsValues?.Fined ?? false,
                Offence = sdk.GamePlay?.FinedEvent?.Amount > 0 ? sdk.GamePlay.FinedEvent.Offence.ToString() : "",
                Amount = sdk.GamePlay?.FinedEvent?.Amount ?? 0,
            },
            Truck = new TruckState
            {
                Id = constants?.Id ?? "",
                EngineRpm = rpm,
                EngineEnabled = legacy?.Drivetrain?.EngineEnabled ?? false,
                GearDashboard = dashboard?.GearDashboards ?? 0,
                ForwardGearCount = (int)(constants?.MotorValues?.ForwardGearCount ?? 0),
                SpeedMph = legacy?.Drivetrain?.SpeedMph ?? 0f,
                FuelAverageConsumption = dashboard?.FuelValue?.AverageConsumption ?? 0f,
                FuelRange = dashboard?.FuelValue?.Range ?? 0f,
                Position = new WorldPosition
                {
                    X = placement?.Position?.X ?? 0d,
                    Y = placement?.Position?.Y ?? 0d,
                    Z = placement?.Position?.Z ?? 0d,
                },
                Heading = placement?.Orientation?.Heading ?? 0f,
                BlinkerLeftOn = lights?.BlinkerLeftOn ?? false,
                BlinkerRightOn = lights?.BlinkerRightOn ?? false,
                Damage = new TruckDamage
                {
                    Cabin = damage?.Cabin ?? 0f,
                    Chassis = damage?.Chassis ?? 0f,
                    Engine = damage?.Engine ?? 0f,
                    Transmission = damage?.Transmission ?? 0f,
                    WheelsAverage = damage?.WheelsAvg ?? 0f,
                },
            },
            Job = new JobState
            {
                CargoId = job?.CargoValues?.Id ?? "",
                CompanySourceId = job?.CompanySourceId ?? "",
                CitySourceId = job?.CitySourceId ?? "",
                CitySource = job?.CitySource ?? "",
                CompanyDestinationId = job?.CompanyDestinationId ?? "",
                CityDestinationId = job?.CityDestinationId ?? "",
                CityDestination = job?.CityDestination ?? "",
                RemainingDeliveryTime = job?.RemainingDeliveryTime?.Time ?? TimeSpan.Zero,
                OnJob = sdk.SpecialEventsValues?.OnJob ?? false,
            },
            Navigation = new NavigationState
            {
                Distance = navigation?.NavigationDistance ?? 0f,
                Time = navigation?.NavigationTime ?? TimeSpan.Zero,
                SpeedLimitMph = legacy?.Job?.SpeedLimitMph ?? 0f,
            },
        };
    }
}
