using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

public sealed class VerticalSliceBridgeTests
{
    [Fact]
    public void Local_vertical_slice_places_tower_sends_creep_and_advances_to_expected_state()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var place = simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 0));
        var send = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);
        for (var index = 0; index < 3; index++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        var events = simulation.DrainEvents();

        Assert.True(place.Accepted);
        Assert.True(send.Accepted);
        Assert.Equal(new SimulationTick(3), snapshot.Tick);
        Assert.Single(snapshot.Towers);
        Assert.Equal(65, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(11, snapshot.Players.Get(new PlayerId(1)).Income.Amount);
        Assert.Contains(events, simulationEvent => simulationEvent is TowerPlacedEvent);
        Assert.Contains(events, simulationEvent => simulationEvent is CreepSpawnedEvent);
    }

    [Fact]
    public void Bridge_rejects_invalid_path_or_affordability_without_duplicate_rules_in_unity()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var blocking = simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(0, 4));

        Assert.False(blocking.Accepted);
        Assert.Equal(CommandRejectionReason.PathBlocked, blocking.RejectionReason);
        Assert.Empty(simulation.GetSnapshot().Towers);
    }

    [Fact]
    public void Bridge_reports_occupied_cells_for_touch_feedback()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 0)).Accepted);

        var occupied = simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 0));

        Assert.False(occupied.Accepted);
        Assert.Equal(CommandRejectionReason.CellOccupied, occupied.RejectionReason);
    }

    [Fact]
    public void Bridge_reset_restores_development_slice_state()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);
        simulation.AdvanceOneTick();

        simulation.Reset();

        var snapshot = simulation.GetSnapshot();
        Assert.Equal(new SimulationTick(0), snapshot.Tick);
        Assert.Equal(100, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Empty(snapshot.Creeps);
        Assert.Empty(snapshot.Towers);
    }

    [Fact]
    public void Bridge_sells_last_tower_and_refunds_gold()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 0)).Accepted);

        var sell = simulation.SellLastTower(new PlayerId(1));
        var snapshot = simulation.GetSnapshot();

        Assert.True(sell.Accepted);
        Assert.Empty(snapshot.Towers);
        Assert.Equal(87, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
    }
}
