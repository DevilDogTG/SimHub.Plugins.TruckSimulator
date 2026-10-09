namespace DevDogs.TruckSimulator.Core.Tests;

public class RangeMathTests
{
    [Theory]
    [InlineData(55f, 50f, 60f, 0.5f)]
    [InlineData(52.5f, 50f, 60f, 0.25f)]
    public void FractionOfRange_InputInsideRange_ReturnsLinearPosition(
        float input,
        float min,
        float max,
        float expected)
    {
        var result = RangeMath.FractionOfRange(input, min, max);

        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Theory]
    [InlineData(40f)]
    [InlineData(50f)]
    public void FractionOfRange_InputAtOrBelowMin_ReturnsZero(float input)
    {
        var result = RangeMath.FractionOfRange(input, 50f, 60f);

        result.Should().Be(0f);
    }

    [Theory]
    [InlineData(60f)]
    [InlineData(75f)]
    public void FractionOfRange_InputAtOrAboveMax_ReturnsOne(float input)
    {
        var result = RangeMath.FractionOfRange(input, 50f, 60f);

        result.Should().Be(1f);
    }

    [Theory]
    [InlineData(49f, 0f)]
    [InlineData(50f, 0f)]
    [InlineData(51f, 1f)]
    public void FractionOfRange_EmptyRange_StepsFromZeroToOne(
        float input,
        float expected)
    {
        var result = RangeMath.FractionOfRange(input, 50f, 50f);

        result.Should().Be(expected);
    }
}
