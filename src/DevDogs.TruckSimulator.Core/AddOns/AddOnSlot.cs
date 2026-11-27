using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DevDogs.TruckSimulator.Core.AddOns;

/// <summary>
/// An add-on that offers its settings page as a tab on this plugin's page (docs/adr/ADR-0007).
/// </summary>
/// <param name="Id">The add-on's key in the slot; tabs are ordered by it.</param>
/// <param name="Title">The tab's header (the id when the add-on gave no title).</param>
/// <param name="Version">The add-on's version, when it gave one.</param>
/// <param name="CreateSettings">
/// The delegate that builds the add-on's settings control (a <c>Func&lt;System.Windows.Controls.Control&gt;</c>; kept
/// as an object here so this assembly needs no WPF).
/// </param>
/// <param name="Entry">
/// The add-on's entry object itself: a new object means the add-on was created again (SimHub does that on every game
/// switch) and its tab content must be rebuilt.
/// </param>
public sealed record AddOn(string Id, string Title, string? Version, Delegate CreateSettings, object Entry);

/// <summary>
/// Reads the add-ons from the AppDomain data slot other DevDogs plugins register in (docs/adr/ADR-0007). Only BCL types
/// cross between the plugins, so neither references the other's assembly.
/// </summary>
public static class AddOnSlot
{
    /// <summary>
    /// The AppDomain data slot: a <see cref="ConcurrentDictionary{TKey, TValue}"/> of add-on id to an entry
    /// (<see cref="IDictionary{TKey, TValue}"/> of string to object) with <c>title</c>, <c>createSettings</c> and,
    /// optionally, <c>version</c>. The first add-on creates it; this plugin only reads it.
    /// </summary>
    public const string SlotName = "DevDogs.TruckSimulator.AddOns.v1";

    /// <summary>
    /// The add-ons registered now, ordered by id.
    /// </summary>
    /// <returns>The add-ons; none when no add-on has registered.</returns>
    public static IReadOnlyList<AddOn> Read() => Read(AppDomain.CurrentDomain.GetData(SlotName));

    /// <summary>
    /// The add-ons in a slot object, ordered by id. Entries without a settings delegate, or of another shape, are left
    /// out; keys this version does not know are ignored.
    /// </summary>
    /// <param name="slot">The slot's value, or null when it does not exist.</param>
    /// <returns>The add-ons.</returns>
    public static IReadOnlyList<AddOn> Read(object? slot)
    {
        if (slot is not ConcurrentDictionary<string, object> entries)
        {
            return [];
        }

        var result = new List<AddOn>();
        foreach (var pair in entries.ToArray().OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (pair.Value is not IDictionary<string, object> values
                || !values.TryGetValue("createSettings", out var create)
                || create is not Delegate createSettings)
            {
                continue;
            }

            var title = values.TryGetValue("title", out var t) && t is string s && s.Length > 0 ? s : pair.Key;
            var version = values.TryGetValue("version", out var v) ? v as string : null;
            result.Add(new AddOn(pair.Key, title, version, createSettings, pair.Value));
        }

        return result;
    }
}
