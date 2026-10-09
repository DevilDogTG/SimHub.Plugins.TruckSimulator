using DevDogs.TruckSimulator.Core.Localisation;

namespace DevDogs.TruckSimulator.Core.Tests.Localisation;

public class AsciiTextTests
{
    [Theory]
    [InlineData("Berlin", "Berlin")]
    [InlineData("Kraków", "Krakow")]
    [InlineData("Łódź", "Lodz")]
    [InlineData("Düsseldorf", "Dusseldorf")]
    [InlineData("Malmö", "Malmo")]
    [InlineData("Ålesund", "Alesund")]
    [InlineData("København", "Kobenhavn")]
    [InlineData("Großbritannien", "Grossbritannien")]
    [InlineData("İstanbul", "Istanbul")]
    [InlineData("Niš", "Nis")]
    public void Fold_LatinText_RemovesAccentsAndReplacesSpecialLetters(
        string text,
        string expected)
    {
        var result = AsciiText.Fold(text);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Москва")]
    public void Fold_EmptyOrNonLatinText_ReturnsEmpty(string text)
    {
        var result = AsciiText.Fold(text);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Fold_NonBreakingSpace_BecomesSpace()
    {
        var result = AsciiText.Fold("Den Haag");

        result.Should().Be("Den Haag");
    }
}
