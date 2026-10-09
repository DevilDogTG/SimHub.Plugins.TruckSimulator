namespace DevDogs.TruckSimulator.Core;

/// <summary>
/// Numeric helpers shared by the telemetry calculators.
/// </summary>
public static class RangeMath
{
    /// <summary>
    /// Expresses <paramref name="input"/> as a fraction of the range from <paramref name="min"/> to
    /// <paramref name="max"/>, clamped to 0..1.
    /// </summary>
    /// <param name="input">The value to convert.</param>
    /// <param name="min">The value that maps to 0.</param>
    /// <param name="max">The value that maps to 1.</param>
    /// <returns>
    /// 0 at or below <paramref name="min"/>, 1 at or above <paramref name="max"/>, otherwise the linear
    /// position in between. Returns 0 for an empty or inverted range.
    /// </returns>
    public static float FractionOfRange(
        float input,
        float min,
        float max)
    {
        if (max <= min || input <= min)
        {
            return 0f;
        }

        if (input >= max)
        {
            return 1f;
        }

        return (input - min) / (max - min);
    }
}
