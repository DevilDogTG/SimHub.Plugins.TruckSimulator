namespace DevDogs.TruckSimulator.Core.GameConfig;

/// <summary>
/// The "Automatic high beams" plugin setting: where <c>Lights.AutoHighBeam</c> comes from.
/// </summary>
public enum AutoHighBeamMode
{
    /// <summary>Read the game's option from the loaded profile's <c>config.cfg</c>.</summary>
    AutoDetect = 0,

    /// <summary>Always report the game's automatic high beams as on.</summary>
    AlwaysOn = 1,

    /// <summary>Always report the game's automatic high beams as off.</summary>
    AlwaysOff = 2,
}

/// <summary>
/// Applies the "Automatic high beams" setting.
/// </summary>
public static class AutoHighBeamSetting
{
    /// <summary>
    /// Works out <c>Lights.AutoHighBeam</c>.
    /// </summary>
    /// <param name="mode">The setting.</param>
    /// <param name="fromProfile">
    /// The option read from the profile's <c>config.cfg</c>; <see langword="null"/> when unknown (no
    /// profile found, no file, the game not running).
    /// </param>
    /// <returns>The value to publish; unknown counts as off.</returns>
    public static bool Resolve(
        AutoHighBeamMode mode,
        bool? fromProfile) => mode switch
        {
            AutoHighBeamMode.AlwaysOn => true,
            AutoHighBeamMode.AlwaysOff => false,
            _ => fromProfile == true,
        };
}
