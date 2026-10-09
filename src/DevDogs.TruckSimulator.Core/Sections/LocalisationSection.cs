using System;
using DevDogs.TruckSimulator.Core.Localisation;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// The job's source and destination city and country names in the user's chosen language.
/// <c>L.</c> properties are translated; <c>L.A.</c> properties are the same names in ASCII only.
/// </summary>
/// <param name="cityNames">The translated names for the chosen language.</param>
public sealed class LocalisationSection(ICityNameSource cityNames) : ITelemetrySection
{
    /// <summary>Property: translated source city.</summary>
    public const string CitySource = "L.Job.CitySource";

    /// <summary>Property: translated source country.</summary>
    public const string CountrySource = "L.Job.CountrySource";

    /// <summary>Property: translated destination city.</summary>
    public const string CityDestination = "L.Job.CityDestination";

    /// <summary>Property: translated destination country.</summary>
    public const string CountryDestination = "L.Job.CountryDestination";

    /// <summary>Property: the source city name as the game reports it, in ASCII.</summary>
    public const string CitySourceFromSdkAscii = "L.A.Job.CitySourceFromSDK";

    /// <summary>Property: the destination city name as the game reports it, in ASCII.</summary>
    public const string CityDestinationFromSdkAscii = "L.A.Job.CityDestinationFromSDK";

    /// <summary>Property: translated source city, in ASCII.</summary>
    public const string CitySourceAscii = "L.A.Job.CitySource";

    /// <summary>Property: translated destination city, in ASCII.</summary>
    public const string CityDestinationAscii = "L.A.Job.CityDestination";

    /// <summary>Property: translated source country, in ASCII.</summary>
    public const string CountrySourceAscii = "L.A.Job.CountrySource";

    /// <summary>Property: translated destination country, in ASCII.</summary>
    public const string CountryDestinationAscii = "L.A.Job.CountryDestination";

    /// <inheritdoc />
    public void Register(ISectionHost host)
    {
        string[] names =
        [
            CitySource, CountrySource, CityDestination, CountryDestination,
            CitySourceFromSdkAscii, CityDestinationFromSdkAscii,
            CitySourceAscii, CityDestinationAscii, CountrySourceAscii, CountryDestinationAscii,
        ];

        foreach (var name in names)
        {
            host.AddProperty(name, "");
        }
    }

    /// <inheritdoc />
    public void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output)
    {
        var job = telemetry.Job;
        var source = Resolve(telemetry.Game, job.CitySourceId, job.CitySource);
        var destination = Resolve(telemetry.Game, job.CityDestinationId, job.CityDestination);

        output.SetProperty(CitySource, source.Name);
        output.SetProperty(CountrySource, source.Country);
        output.SetProperty(CitySourceAscii, source.NameAscii);
        output.SetProperty(CountrySourceAscii, source.CountryAscii);
        output.SetProperty(CitySourceFromSdkAscii, AsciiText.Fold(job.CitySource));

        output.SetProperty(CityDestination, destination.Name);
        output.SetProperty(CountryDestination, destination.Country);
        output.SetProperty(CityDestinationAscii, destination.NameAscii);
        output.SetProperty(CountryDestinationAscii, destination.CountryAscii);
        output.SetProperty(CityDestinationFromSdkAscii, AsciiText.Fold(job.CityDestination));
    }

    /// <summary>
    /// Finds a city's translated names, falling back to the name the game reports when the city is
    /// missing from the localisation data (for example a city from a newer map DLC).
    /// </summary>
    /// <param name="game">The running game.</param>
    /// <param name="cityId">The city id; empty when there is no job.</param>
    /// <param name="gameName">The city name the game reports.</param>
    /// <returns>The names to publish; all empty when there is no city.</returns>
    private LocalisedCity Resolve(
        TruckGame game,
        string cityId,
        string gameName)
    {
        if (cityId.Length == 0)
        {
            return new LocalisedCity();
        }

        return cityNames.TryGet(game, cityId, out var city) && city is not null
            ? city
            : new LocalisedCity { Name = gameName, NameAscii = AsciiText.Fold(gameName) };
    }
}
