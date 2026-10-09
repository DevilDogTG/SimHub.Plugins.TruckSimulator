using System;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Sections;

/// <summary>
/// Declares a section's properties, events and actions with SimHub at start-up.
/// Names are relative; the plugin adds its <c>DDTruckPlugin.</c> prefix.
/// </summary>
public interface ISectionHost
{
    /// <summary>
    /// Declares a property and its value before the first update.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The value shown until the first update.</param>
    void AddProperty(
        string name,
        object defaultValue);

    /// <summary>
    /// Declares an event that the section may trigger.
    /// </summary>
    /// <param name="name">The event name.</param>
    void AddEvent(string name);

    /// <summary>
    /// Declares an action that users can bind to a control.
    /// </summary>
    /// <param name="name">The action name.</param>
    /// <param name="body">Runs when the action fires; receives the output for setting properties or triggering events.</param>
    void AddAction(
        string name,
        Action<ISectionOutput> body);
}

/// <summary>
/// Receives the values and events a section produces.
/// </summary>
public interface ISectionOutput
{
    /// <summary>
    /// Sets a declared property's value.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The new value.</param>
    void SetProperty(
        string name,
        object value);

    /// <summary>
    /// Triggers a declared event.
    /// </summary>
    /// <param name="name">The event name.</param>
    void TriggerEvent(string name);
}

/// <summary>
/// A group of related properties, events and actions.
/// </summary>
public interface ISection
{
    /// <summary>
    /// Declares the section's properties, events and actions.
    /// </summary>
    /// <param name="host">The host to declare them with.</param>
    void Register(ISectionHost host);
}

/// <summary>
/// A section computed from game telemetry; updated only while a supported game is running.
/// </summary>
public interface ITelemetrySection : ISection
{
    /// <summary>
    /// Computes the section's values for one update tick.
    /// </summary>
    /// <param name="telemetry">The current telemetry snapshot.</param>
    /// <param name="now">The current time, used for time-based latches.</param>
    /// <param name="output">Receives property values and events.</param>
    void Update(
        TruckTelemetry telemetry,
        DateTime now,
        ISectionOutput output);
}
