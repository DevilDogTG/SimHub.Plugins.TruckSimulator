using System;
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
        var damage = current?.DamageValues;
        var job = sdk.JobValues;
        var navigation = sdk.NavigationValues;

        return new TruckTelemetry
        {
            Game = game,
            NextRestStop = sdk.CommonValues?.NextRestStop?.Time ?? TimeSpan.Zero,
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
