using System;
using System.Collections.Generic;
using System.Linq;

namespace DevDogs.TruckSimulator.Core.AddOns;

/// <summary>
/// The page's version line: this plugin's version, then each add-on's name and version (docs/adr/ADR-0007).
/// </summary>
public static class AddOnVersions
{
    /// <summary>What goes between the items of the line.</summary>
    public const string Separator = " · ";

    /// <summary>
    /// The add-ons to name on the line, each as "name version", ordered by id. Entries whose ids share the part before
    /// the first <c>.</c> are one add-on with several tabs (Navigation Dash's <c>navigationdash</c> and
    /// <c>navigationdash.reports</c>): it is named by the first entry's title and the first version in the group. An
    /// add-on that gave no version is left out.
    /// </summary>
    /// <param name="addOns">The add-ons, ordered by id as <see cref="AddOnSlot.Read()"/> returns them.</param>
    /// <returns>The items.</returns>
    public static IReadOnlyList<string> Items(IReadOnlyList<AddOn> addOns) =>
        addOns
            .GroupBy(a => Root(a.Id), StringComparer.Ordinal)
            .Select(g => (Name: g.First().Title, Version: g.Select(a => a.Version).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))))
            .Where(item => item.Version is not null)
            .Select(item => item.Name + " " + item.Version!.Trim())
            .ToList();

    /// <summary>
    /// The version line, for example <c>Version 1.4.0 · Navigation Dash 1.5.1</c>; just <c>Version 1.4.0</c> without
    /// add-ons.
    /// </summary>
    /// <param name="version">This plugin's version.</param>
    /// <param name="addOns">The add-ons, ordered by id.</param>
    /// <returns>The line.</returns>
    public static string Line(string version, IReadOnlyList<AddOn> addOns) =>
        string.Join(Separator, new[] { "Version " + version }.Concat(Items(addOns)));

    /// <summary>The part of an add-on id before its first <c>.</c>: the add-on its tab belongs to.</summary>
    private static string Root(string id)
    {
        var dot = id.IndexOf('.');
        return dot < 0 ? id : id.Substring(0, dot);
    }
}
