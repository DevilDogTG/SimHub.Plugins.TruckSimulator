using System;
using DevDogs.TruckSimulator.Core.AddOns;

namespace DevDogs.TruckSimulator.Core.Tests.AddOns;

public class AddOnVersionsTests
{
    private static readonly Func<object> Create = () => new object();

    private static AddOn AddOn(string id, string title, string? version) => new(id, title, version, Create, new object());

    [Fact]
    public void Line_NoAddOns_IsTheVersionAlone()
    {
        AddOnVersions.Line("1.4.0", []).Should().Be("Version 1.4.0");
    }

    [Fact]
    public void Line_AnAddOn_FollowsTheVersion()
    {
        AddOnVersions.Line("1.4.0", [AddOn("navigationdash", "Navigation Dash", "1.5.1")])
            .Should().Be("Version 1.4.0 · Navigation Dash 1.5.1");
    }

    [Fact]
    public void Items_TabsOfOneAddOn_AreNamedOnce()
    {
        var addOns = new[]
        {
            AddOn("navigationdash", "Navigation Dash", "1.5.1"),
            AddOn("navigationdash.reports", "Map reports", "1.5.1"),
        };

        AddOnVersions.Items(addOns).Should().Equal("Navigation Dash 1.5.1");
    }

    [Fact]
    public void Items_SeveralAddOns_InIdOrder()
    {
        var addOns = new[]
        {
            AddOn("alpha", "Alpha", "2.0.0"),
            AddOn("navigationdash", "Navigation Dash", "1.5.1"),
            AddOn("navigationdash.reports", "Map reports", "1.5.1"),
            AddOn("zulu", "Zulu", "0.1.0"),
        };

        AddOnVersions.Items(addOns).Should().Equal("Alpha 2.0.0", "Navigation Dash 1.5.1", "Zulu 0.1.0");
    }

    [Fact]
    public void Items_NoVersion_IsLeftOut()
    {
        var addOns = new[] { AddOn("alpha", "Alpha", null), AddOn("bravo", "Bravo", " "), AddOn("zulu", "Zulu", "0.1.0") };

        AddOnVersions.Items(addOns).Should().Equal("Zulu 0.1.0");
    }

    [Fact]
    public void Items_RootWithoutVersion_TakesTheGroupsFirstVersion()
    {
        var addOns = new[]
        {
            AddOn("navigationdash", "Navigation Dash", null),
            AddOn("navigationdash.reports", "Map reports", "1.5.1"),
        };

        AddOnVersions.Items(addOns).Should().Equal("Navigation Dash 1.5.1");
    }

    [Fact]
    public void Items_OnlyASubTab_IsNamedByItsTitle()
    {
        AddOnVersions.Items([AddOn("navigationdash.reports", "Map reports", "1.5.1")])
            .Should().Equal("Map reports 1.5.1");
    }
}
