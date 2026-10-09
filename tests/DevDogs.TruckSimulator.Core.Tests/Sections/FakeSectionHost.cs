using DevDogs.TruckSimulator.Core.Sections;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

/// <summary>
/// Records what a section declares and outputs, standing in for SimHub.
/// </summary>
internal sealed class FakeSectionHost : ISectionHost, ISectionOutput
{
    public Dictionary<string, object> Defaults { get; } = [];

    public HashSet<string> DeclaredEvents { get; } = [];

    public Dictionary<string, Action<ISectionOutput>> Actions { get; } = [];

    public Dictionary<string, object> Properties { get; } = [];

    public List<string> TriggeredEvents { get; } = [];

    public void AddProperty<T>(
        string name,
        T defaultValue)
        where T : notnull => Defaults[name] = defaultValue;

    public void AddEvent(string name) => DeclaredEvents.Add(name);

    public void AddAction(
        string name,
        Action<ISectionOutput> body) => Actions[name] = body;

    public void SetProperty(
        string name,
        object value) => Properties[name] = value;

    public void TriggerEvent(string name) => TriggeredEvents.Add(name);

    public void RunAction(string name) => Actions[name](this);
}
