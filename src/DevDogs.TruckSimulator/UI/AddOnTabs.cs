using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace DevDogs.TruckSimulator.UI;

/// <summary>
/// Shows other DevDogs plugins' settings as tabs on this plugin's page (spike, plan <c>addon-tab</c>). An add-on puts
/// an entry into the AppDomain data slot <see cref="SlotName"/>; only BCL and WPF types cross, so neither plugin
/// references the other's assembly.
/// </summary>
/// <remarks>
/// The slot holds a <see cref="ConcurrentDictionary{TKey, TValue}"/> of add-on id to an
/// <see cref="IDictionary{TKey, TValue}"/> with <c>title</c> (string), <c>version</c> (string), <c>status</c>
/// (<see cref="Func{TResult}"/> of string) and <c>createSettings</c> (<see cref="Func{TResult}"/> of
/// <see cref="Control"/>). The add-on creates the slot when it is missing and replaces its entry when SimHub creates
/// the plugin again (every game switch), so a changed entry object means the tab's content is rebuilt.
/// </remarks>
internal sealed class AddOnTabs
{
    /// <summary>The AppDomain data slot the add-ons register in.</summary>
    public const string SlotName = "DevDogs.TruckSimulator.AddOns.v1";

    private readonly TabControl _tabs;
    private readonly Dictionary<string, (TabItem Tab, object Entry)> _shown = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="AddOnTabs"/> class.
    /// </summary>
    /// <param name="tabs">The page's tab control; add-on tabs go after its own tabs.</param>
    public AddOnTabs(TabControl tabs)
    {
        _tabs = tabs;
    }

    /// <summary>
    /// Adds a tab for each new add-on, rebuilds the tab of an add-on whose entry changed and removes the tabs of
    /// add-ons that left. Runs on the page's UI thread.
    /// </summary>
    public void Sync()
    {
        var entries = Read();
        foreach (var id in _shown.Keys.Where(id => !entries.ContainsKey(id)).ToList())
        {
            var tab = _shown[id].Tab;
            if (tab.IsSelected)
            {
                _tabs.SelectedIndex = 0;
            }

            _tabs.Items.Remove(tab);
            _shown.Remove(id);
        }

        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (entry.Value is not IDictionary<string, object> values)
            {
                continue;
            }

            var title = values.TryGetValue("title", out var t) && t is string s && s.Length > 0 ? s : entry.Key;
            if (_shown.TryGetValue(entry.Key, out var shown))
            {
                if (ReferenceEquals(shown.Entry, entry.Value))
                {
                    continue;
                }

                shown.Tab.Header = title;
                shown.Tab.Content = Build(values);
                _shown[entry.Key] = (shown.Tab, entry.Value);
                continue;
            }

            var tab = new TabItem { Header = title, Content = Build(values) };
            _tabs.Items.Add(tab);
            _shown[entry.Key] = (tab, entry.Value);
        }
    }

    /// <summary>
    /// The add-on entries now in the slot, or none when no add-on has registered.
    /// </summary>
    /// <returns>Add-on id to entry.</returns>
    private static Dictionary<string, object> Read() =>
        AppDomain.CurrentDomain.GetData(SlotName) is ConcurrentDictionary<string, object> slot
            ? new Dictionary<string, object>(slot, StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// The add-on's settings control, or a line saying why there is none.
    /// </summary>
    /// <param name="values">The add-on's entry.</param>
    /// <returns>The tab's content.</returns>
    private static object Build(IDictionary<string, object> values)
    {
        if (!values.TryGetValue("createSettings", out var f) || f is not Func<Control> create)
        {
            return new TextBlock { Text = "This add-on has no settings page.", Margin = new System.Windows.Thickness(12) };
        }

        try
        {
            return create();
        }
        catch (Exception ex)
        {
            return new TextBlock
            {
                Text = $"The add-on's settings could not be shown: {ex.Message}",
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Margin = new System.Windows.Thickness(12),
            };
        }
    }
}
