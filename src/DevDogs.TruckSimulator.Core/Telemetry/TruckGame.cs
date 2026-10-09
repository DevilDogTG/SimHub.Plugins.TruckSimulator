namespace DevDogs.TruckSimulator.Core.Telemetry;

/// <summary>
/// The SCS truck games this plugin supports.
/// </summary>
public enum TruckGame
{
    /// <summary>Not a supported game.</summary>
    Unknown = 0,

    /// <summary>Euro Truck Simulator 2.</summary>
    Ets2 = 1,

    /// <summary>American Truck Simulator.</summary>
    Ats = 2,
}

/// <summary>
/// Maps SimHub game names to <see cref="TruckGame"/>.
/// </summary>
public static class TruckGames
{
    /// <summary>
    /// Resolves the game from the name SimHub reports in <c>GameData.GameName</c>.
    /// </summary>
    /// <param name="simHubGameName">The SimHub game name, for example <c>ETS2</c> or <c>ATS</c>.</param>
    /// <returns>The matching game, or <see cref="TruckGame.Unknown"/> for any other name.</returns>
    public static TruckGame FromSimHubName(string? simHubGameName) => simHubGameName switch
    {
        "ETS2" => TruckGame.Ets2,
        "ATS" => TruckGame.Ats,
        _ => TruckGame.Unknown,
    };
}
