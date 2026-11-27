using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DevDogs.TruckSimulator.Core.AddOns;

namespace DevDogs.TruckSimulator.UI;

/// <summary>
/// Shows other DevDogs plugins' settings pages as tabs after this page's own tabs (docs/adr/ADR-0007). The add-ons come
/// from <see cref="AddOnSlot"/>; this class only keeps the tabs in step with it.
/// </summary>
internal sealed class AddOnTabs
{
    private readonly TabControl _tabs;
    private readonly Dictionary<string, (TabItem Tab, object Entry)> _shown = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="AddOnTabs"/> class.
    /// </summary>
    /// <param name="tabs">The page's tab control.</param>
    public AddOnTabs(TabControl tabs)
    {
        _tabs = tabs;
    }

    /// <summary>
    /// Adds a tab for each new add-on, rebuilds the tab of an add-on that registered again (a new entry object) and
    /// removes the tabs of add-ons that left. Runs on the page's UI thread.
    /// </summary>
    public void Sync()
    {
        var addOns = AddOnSlot.Read();
        var ids = new HashSet<string>(addOns.Select(a => a.Id), StringComparer.Ordinal);
        foreach (var id in _shown.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            var tab = _shown[id].Tab;
            if (tab.IsSelected)
            {
                _tabs.SelectedIndex = 0;
            }

            _tabs.Items.Remove(tab);
            _shown.Remove(id);
        }

        foreach (var addOn in addOns)
        {
            if (_shown.TryGetValue(addOn.Id, out var shown))
            {
                if (ReferenceEquals(shown.Entry, addOn.Entry))
                {
                    continue;
                }

                shown.Tab.Header = addOn.Title;
                shown.Tab.Content = Build(addOn);
                _shown[addOn.Id] = (shown.Tab, addOn.Entry);
                continue;
            }

            var added = new TabItem { Header = addOn.Title, Content = Build(addOn) };
            _tabs.Items.Add(added);
            _shown[addOn.Id] = (added, addOn.Entry);
        }
    }

    /// <summary>
    /// The add-on's settings control, or a line saying why it can't be shown.
    /// </summary>
    /// <param name="addOn">The add-on.</param>
    /// <returns>The tab's content.</returns>
    private static object Build(AddOn addOn)
    {
        if (addOn.CreateSettings is not Func<Control> create)
        {
            return Message($"{addOn.Title} offers a settings page this version can't show.");
        }

        try
        {
            return create();
        }
        catch (Exception ex)
        {
            // another plugin's code: show its failure in its tab instead of breaking this page
            return Message($"{addOn.Title}'s settings could not be shown: {ex.Message}");
        }
    }

    /// <summary>
    /// A wrapped line of text for a tab.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text block.</returns>
    private static TextBlock Message(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
}
