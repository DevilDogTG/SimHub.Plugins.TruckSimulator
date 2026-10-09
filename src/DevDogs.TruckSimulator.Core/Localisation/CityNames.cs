using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Localisation;

/// <summary>
/// A city's name and country in the user's chosen language, with ASCII-only variants for displays
/// that can't show other characters.
/// </summary>
public sealed record LocalisedCity
{
    /// <summary>Gets the translated city name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Gets the translated city name using ASCII characters only.</summary>
    public string NameAscii { get; init; } = "";

    /// <summary>Gets the translated country name.</summary>
    public string Country { get; init; } = "";

    /// <summary>Gets the translated country name using ASCII characters only.</summary>
    public string CountryAscii { get; init; } = "";
}

/// <summary>
/// Looks up translated city names for the user's chosen language.
/// </summary>
public interface ICityNameSource
{
    /// <summary>
    /// Finds a city's translated names.
    /// </summary>
    /// <param name="game">The game the city belongs to.</param>
    /// <param name="cityId">The game's city id, for example <c>berlin</c>.</param>
    /// <param name="city">The translated names, when found.</param>
    /// <returns><see langword="true"/> when the city is known.</returns>
    bool TryGet(
        TruckGame game,
        string cityId,
        out LocalisedCity? city);
}

/// <summary>
/// A source with no translations, used until localisation data is loaded.
/// </summary>
public sealed class EmptyCityNameSource : ICityNameSource
{
    /// <summary>Gets the shared instance.</summary>
    public static EmptyCityNameSource Instance { get; } = new();

    /// <inheritdoc />
    public bool TryGet(
        TruckGame game,
        string cityId,
        out LocalisedCity? city)
    {
        city = null;
        return false;
    }
}
