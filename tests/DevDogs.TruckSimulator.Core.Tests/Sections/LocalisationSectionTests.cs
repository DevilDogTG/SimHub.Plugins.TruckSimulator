using DevDogs.TruckSimulator.Core.Localisation;
using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class LocalisationSectionTests
{
    /// <summary>Knows one ETS2 city, translated to Polish.</summary>
    private sealed class PolishNames : ICityNameSource
    {
        public bool TryGet(
            TruckGame game,
            string cityId,
            out LocalisedCity? city)
        {
            city = game == TruckGame.Ets2 && cityId == "krakow"
                ? new LocalisedCity { Name = "Kraków", NameAscii = "Krakow", Country = "Polska", CountryAscii = "Polska" }
                : null;
            return city is not null;
        }
    }

    private static TruckTelemetry Job(
        string sourceId,
        string sourceName,
        TruckGame game = TruckGame.Ets2) => new()
    {
        Game = game,
        Job = new JobState { CitySourceId = sourceId, CitySource = sourceName, CityDestinationId = "", CityDestination = "" },
    };

    private static FakeSectionHost Run(
        TruckTelemetry telemetry,
        ICityNameSource names)
    {
        var host = new FakeSectionHost();
        new LocalisationSection(names).Update(telemetry, DateTime.UnixEpoch, host);
        return host;
    }

    [Fact]
    public void Register_DeclaresTenEmptyProperties()
    {
        var host = new FakeSectionHost();

        new LocalisationSection(EmptyCityNameSource.Instance).Register(host);

        host.Defaults.Should().HaveCount(10).And.AllSatisfy(p => p.Value.Should().Be(""));
    }

    [Fact]
    public void Update_KnownCity_PublishesTranslatedNames()
    {
        var host = Run(Job("krakow", "Krakow"), new PolishNames());

        host.Properties.Should().Contain(LocalisationSection.CitySource, "Kraków");
        host.Properties.Should().Contain(LocalisationSection.CountrySource, "Polska");
        host.Properties.Should().Contain(LocalisationSection.CitySourceAscii, "Krakow");
        host.Properties.Should().Contain(LocalisationSection.CountrySourceAscii, "Polska");
    }

    [Fact]
    public void Update_UnknownCity_FallsBackToGameName()
    {
        var host = Run(Job("lodz", "Łódź"), new PolishNames());

        host.Properties.Should().Contain(LocalisationSection.CitySource, "Łódź");
        host.Properties.Should().Contain(LocalisationSection.CitySourceAscii, "Lodz");
        host.Properties.Should().Contain(LocalisationSection.CountrySource, "");
    }

    [Fact]
    public void Update_SameCityIdInOtherGame_FallsBackToGameName()
    {
        var host = Run(Job("krakow", "Krakow", TruckGame.Ats), new PolishNames());

        host.Properties.Should().Contain(LocalisationSection.CitySource, "Krakow");
    }

    [Fact]
    public void Update_GameReportedName_PublishedAsAsciiWithoutData()
    {
        var host = Run(Job("lodz", "Łódź"), EmptyCityNameSource.Instance);

        host.Properties.Should().Contain(LocalisationSection.CitySourceFromSdkAscii, "Lodz");
    }

    [Fact]
    public void Update_NoJob_PublishesEmptyNames()
    {
        var host = Run(new TruckTelemetry(), new PolishNames());

        host.Properties.Values.Should().AllBeEquivalentTo("");
    }
}
