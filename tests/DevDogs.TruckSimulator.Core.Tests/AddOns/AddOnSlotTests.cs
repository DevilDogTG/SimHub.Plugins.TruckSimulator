using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DevDogs.TruckSimulator.Core.AddOns;

namespace DevDogs.TruckSimulator.Core.Tests.AddOns;

public class AddOnSlotTests
{
    private static readonly Func<object> Create = () => new object();

    private static Dictionary<string, object> Entry(string? title = "Navigation Dash", string? version = "1.3.0")
    {
        var entry = new Dictionary<string, object> { ["createSettings"] = Create };
        if (title is not null)
        {
            entry["title"] = title;
        }

        if (version is not null)
        {
            entry["version"] = version;
        }

        return entry;
    }

    [Fact]
    public void Read_NoSlot_ReturnsNone()
    {
        AddOnSlot.Read(null).Should().BeEmpty();
        AddOnSlot.Read("not a slot").Should().BeEmpty();
    }

    [Fact]
    public void Read_Entries_ReturnsThemOrderedById()
    {
        var b = Entry("Bravo");
        var a = Entry("Alpha", version: null);
        var slot = new ConcurrentDictionary<string, object> { ["zulu"] = b, ["alpha"] = a };

        var addOns = AddOnSlot.Read(slot);

        addOns.Should().HaveCount(2);
        addOns[0].Should().Be(new AddOn("alpha", "Alpha", null, Create, a));
        addOns[1].Should().Be(new AddOn("zulu", "Bravo", "1.3.0", Create, b));
    }

    [Fact]
    public void Read_NoTitle_UsesTheId()
    {
        var slot = new ConcurrentDictionary<string, object> { ["navigationdash"] = Entry(title: null) };

        AddOnSlot.Read(slot)[0].Title.Should().Be("navigationdash");
    }

    [Fact]
    public void Read_EntriesWithoutASettingsDelegate_AreLeftOut()
    {
        var slot = new ConcurrentDictionary<string, object>
        {
            ["no-delegate"] = new Dictionary<string, object> { ["title"] = "No page" },
            ["wrong-type"] = new Dictionary<string, object> { ["createSettings"] = "not a delegate" },
            ["not-a-dictionary"] = 42,
            ["ok"] = Entry(),
        };

        AddOnSlot.Read(slot).Should().ContainSingle().Which.Id.Should().Be("ok");
    }

    [Fact]
    public void Read_UnknownKeys_AreIgnored()
    {
        var entry = Entry();
        entry["status"] = (Func<string>)(() => "ready");
        entry["somethingNew"] = 1;
        var slot = new ConcurrentDictionary<string, object> { ["ok"] = entry };

        AddOnSlot.Read(slot).Should().ContainSingle().Which.Title.Should().Be("Navigation Dash");
    }

    [Fact]
    public void Read_FromTheAppDomain_FindsWhatAnAddOnRegistered()
    {
        var entry = Entry();
        var slot = new ConcurrentDictionary<string, object> { ["navigationdash"] = entry };
        AppDomain.CurrentDomain.SetData(AddOnSlot.SlotName, slot);
        try
        {
            AddOnSlot.Read().Should().ContainSingle().Which.Entry.Should().BeSameAs(entry);
        }
        finally
        {
            AppDomain.CurrentDomain.SetData(AddOnSlot.SlotName, null);
        }
    }
}
