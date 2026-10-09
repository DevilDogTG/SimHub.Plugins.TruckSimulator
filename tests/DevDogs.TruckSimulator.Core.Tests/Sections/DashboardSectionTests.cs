using DevDogs.TruckSimulator.Core.Sections;

namespace DevDogs.TruckSimulator.Core.Tests.Sections;

public class DashboardSectionTests
{
    [Fact]
    public void Register_DeclaresPropertyWithSettingAndAction()
    {
        var host = new FakeSectionHost();
        var section = new DashboardSection(new PluginSettings { DashUnitMetric = true });

        section.Register(host);

        host.Defaults.Should().Contain(DashboardSection.DisplayUnitMetric, true);
        host.Actions.Should().ContainKey(DashboardSection.SwitchDisplayUnit);
    }

    [Fact]
    public void Update_PublishesCurrentSetting()
    {
        var host = new FakeSectionHost();
        var section = new DashboardSection(new PluginSettings { DashUnitMetric = false });

        section.Update(host);

        host.Properties.Should().Contain(DashboardSection.DisplayUnitMetric, false);
    }

    [Fact]
    public void SwitchDisplayUnit_TogglesSettingAndPublishesIt()
    {
        var settings = new PluginSettings { DashUnitMetric = false };
        var host = new FakeSectionHost();
        var section = new DashboardSection(settings);
        section.Register(host);

        host.RunAction(DashboardSection.SwitchDisplayUnit);

        settings.DashUnitMetric.Should().BeTrue();
        host.Properties.Should().Contain(DashboardSection.DisplayUnitMetric, true);
    }
}
