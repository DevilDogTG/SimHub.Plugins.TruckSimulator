using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Diagnostics;

/// <summary>
/// Lists every value in a <see cref="TruckTelemetry"/> snapshot by its dotted path, for display.
/// Driven by reflection, so fields added to the snapshot appear without changes here.
/// </summary>
public static class SnapshotFields
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _properties = new();

    /// <summary>
    /// Flattens a snapshot into its leaf values, for example <c>Truck.Damage.Cabin</c>.
    /// </summary>
    /// <param name="telemetry">The snapshot.</param>
    /// <returns>Each leaf value with its path, in declaration order.</returns>
    public static IReadOnlyList<KeyValuePair<string, object?>> Flatten(TruckTelemetry telemetry)
    {
        var fields = new List<KeyValuePair<string, object?>>();
        Collect(telemetry, "", fields);
        return fields;
    }

    /// <summary>
    /// Adds the leaf values of <paramref name="value"/>, recursing into nested snapshot records.
    /// </summary>
    /// <param name="value">The object to list.</param>
    /// <param name="prefix">The path of <paramref name="value"/>, ending in a dot, or empty for the root.</param>
    /// <param name="fields">Receives the leaf values.</param>
    private static void Collect(
        object value,
        string prefix,
        List<KeyValuePair<string, object?>> fields)
    {
        foreach (var property in PropertiesOf(value.GetType()))
        {
            var child = property.GetValue(value);
            var path = prefix + property.Name;

            if (child is not null && IsSnapshotRecord(property.PropertyType))
            {
                Collect(child, path + ".", fields);
            }
            else
            {
                fields.Add(new KeyValuePair<string, object?>(path, child));
            }
        }
    }

    /// <summary>
    /// Whether a type is one of the snapshot's own nested records, as opposed to a leaf value.
    /// </summary>
    /// <param name="type">The property type.</param>
    /// <returns><see langword="true"/> for nested snapshot records.</returns>
    private static bool IsSnapshotRecord(Type type) =>
        type.IsClass && type != typeof(string) && type.Namespace == typeof(TruckTelemetry).Namespace;

    /// <summary>
    /// Gets a type's public instance properties in declaration order, cached.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The properties.</returns>
    private static PropertyInfo[] PropertiesOf(Type type) =>
        _properties.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.MetadataToken).ToArray());
}
