using DevDogs.TruckSimulator.Core.Recording;
using DevDogs.TruckSimulator.Core.Sections;
using DevDogs.TruckSimulator.Core.Tests.Sections;

namespace DevDogs.TruckSimulator.Core.Tests.Recording;

/// <summary>
/// Replays recorded telemetry through a section with the recorded timestamps, exactly as the
/// plugin would have run it live.
/// </summary>
internal static class TelemetryReplay
{
    public static FakeSectionHost Run(
        ITelemetrySection section,
        IEnumerable<RecordedTick> ticks)
    {
        var host = new FakeSectionHost();
        section.Register(host);

        foreach (var tick in ticks)
        {
            section.Update(tick.Telemetry, tick.At, host);
        }

        return host;
    }
}
