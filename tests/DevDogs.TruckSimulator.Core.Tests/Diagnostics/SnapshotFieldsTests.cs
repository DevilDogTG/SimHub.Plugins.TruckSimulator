using DevDogs.TruckSimulator.Core.Diagnostics;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.Diagnostics;

public class SnapshotFieldsTests
{
    [Fact]
    public void Flatten_Snapshot_ListsNestedLeavesByDottedPath()
    {
        var telemetry = new TruckTelemetry
        {
            Game = TruckGame.Ats,
            Truck = new TruckState
            {
                SpeedMph = 61.5f,
                Position = new WorldPosition { X = 1.5 },
                Damage = new TruckDamage { Cabin = 0.25f },
            },
        };

        var fields = SnapshotFields.Flatten(telemetry).ToDictionary(f => f.Key, f => f.Value);

        fields.Should().Contain("Game", TruckGame.Ats);
        fields.Should().Contain("Truck.SpeedMph", 61.5f);
        fields.Should().Contain("Truck.Position.X", 1.5d);
        fields.Should().Contain("Truck.Damage.Cabin", 0.25f);
        fields.Should().Contain("Job.CargoId", "");
    }

    [Fact]
    public void Flatten_Snapshot_HasNoNestedRecordsAsLeaves()
    {
        var fields = SnapshotFields.Flatten(new TruckTelemetry());

        fields.Select(f => f.Key).Should().NotContain(["Truck", "Truck.Damage", "Truck.Position", "Job", "Navigation"]);
    }

    [Fact]
    public void Flatten_Snapshot_StartsInDeclarationOrder()
    {
        var keys = SnapshotFields.Flatten(new TruckTelemetry()).Select(f => f.Key).ToList();

        keys.Take(3).Should().Equal("Game", "Truck.Id", "Truck.EngineRpm");
    }
}
