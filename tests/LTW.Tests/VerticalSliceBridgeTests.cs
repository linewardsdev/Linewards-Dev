using System.Linq;
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

        var place = simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1));
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
        Assert.Contains(snapshot.Towers, tower =>
            tower.OwnerId.Equals(new PlayerId(1)) &&
            tower.LaneId.Equals(new LaneId(1)) &&
            tower.Position.Equals(new GridPosition(1, 1)));
        Assert.Equal(65, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(11, snapshot.Players.Get(new PlayerId(1)).Income.Amount);
        Assert.Contains(events, simulationEvent => simulationEvent is TowerPlacedEvent);
        Assert.Contains(events, simulationEvent => simulationEvent is CreepSpawnedEvent);
    }

    [Fact]
    public void Bridge_rejects_invalid_path_or_affordability_without_duplicate_rules_in_unity()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var blocking = simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(3, 0));

        Assert.False(blocking.Accepted);
        Assert.Equal(CommandRejectionReason.PathBlocked, blocking.RejectionReason);
        Assert.Empty(simulation.GetSnapshot().Towers);
    }

    [Fact]
    public void Bridge_reports_occupied_cells_for_touch_feedback()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1)).Accepted);

        var occupied = simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1));

        Assert.False(occupied.Accepted);
        Assert.Equal(CommandRejectionReason.CellOccupied, occupied.RejectionReason);
    }

    [Fact]
    public void Placement_preview_reports_rules_without_mutating_match_state()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var ready = simulation.PreviewPlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1));
        var blocked = simulation.PreviewPlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(3, 0));
        var snapshot = simulation.GetSnapshot();

        Assert.True(ready.Accepted);
        Assert.False(blocked.Accepted);
        Assert.Equal(CommandRejectionReason.PathBlocked, blocked.RejectionReason);
        Assert.Empty(snapshot.Towers);
        Assert.Equal(100, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
    }

    [Fact]
    public void Preview_reports_affordability_and_occupancy_for_tower_palette()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.UtilityTowerId, new GridPosition(1, 1)).Accepted);
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.UtilityTowerId, new GridPosition(2, 1)).Accepted);

        var occupied = simulation.PreviewPlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1));
        var unaffordable = simulation.PreviewPlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.UtilityTowerId, new GridPosition(4, 1));

        Assert.False(occupied.Accepted);
        Assert.Equal(CommandRejectionReason.CellOccupied, occupied.RejectionReason);
        Assert.False(unaffordable.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, unaffordable.RejectionReason);
    }




    [Fact]
    public void Bots_place_profile_towers_before_creating_send_pressure()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        simulation.AdvanceOneTick();

        var snapshot = simulation.GetSnapshot();
        Assert.Contains(snapshot.Towers, tower => tower.OwnerId.Equals(new PlayerId(2)) && tower.TowerId.Equals(SampleVerticalSliceContent.TowerId));
        Assert.Contains(snapshot.Towers, tower => tower.OwnerId.Equals(new PlayerId(3)) && tower.TowerId.Equals(SampleVerticalSliceContent.ControlTowerId));
    }

    [Fact]
    public void Bridge_sells_selected_tower_position()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1)).Accepted);
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.ControlTowerId, new GridPosition(5, 1)).Accepted);

        var sell = simulation.SellTowerAt(new PlayerId(1), new LaneId(1), new GridPosition(1, 1));
        var snapshot = simulation.GetSnapshot();

        Assert.True(sell.Accepted);
        Assert.DoesNotContain(snapshot.Towers, tower => tower.Position.Equals(new GridPosition(1, 1)));
        Assert.Contains(snapshot.Towers, tower => tower.Position.Equals(new GridPosition(5, 1)));
    }

    [Fact]
    public void Bot_diagnostics_expose_profiles_and_recent_decisions()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var initial = simulation.GetBotDiagnostics();
        Assert.Contains(initial.Profiles, profile => profile.PlayerId.Equals(new PlayerId(2)) && profile.Profile == LTW.Simulation.Bots.BotDecisionProfile.Balanced);
        Assert.Contains(initial.Profiles, profile => profile.PlayerId.Equals(new PlayerId(3)) && profile.Profile == LTW.Simulation.Bots.BotDecisionProfile.Defensive);

        for (var tick = 0; tick < 7; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var diagnostics = simulation.GetBotDiagnostics();
        Assert.Contains(diagnostics.RecentDecisions, decision => decision.PlayerId.Equals(new PlayerId(2)) && decision.Quantity == 2);
        Assert.Contains(diagnostics.RecentDecisions, decision => decision.PlayerId.Equals(new PlayerId(3)) && decision.Quantity == 1);
    }

    [Fact]
    public void Bridge_reset_clears_bot_decision_diagnostics()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        simulation.AdvanceOneTick();
        Assert.NotEmpty(simulation.GetBotDiagnostics().RecentDecisions);

        simulation.Reset();

        Assert.Empty(simulation.GetBotDiagnostics().RecentDecisions);
    }

    [Fact]
    public void Sample_content_exposes_placeholder_visual_roles()
    {
        var content = SampleVerticalSliceContent.Create();

        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.TowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.ControlTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.UtilityTowerId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.CreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.BruteCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.SwarmCreepId));
    }

    [Fact]
    public void Sample_content_has_distinct_tower_and_send_pressure_profiles()
    {
        var content = SampleVerticalSliceContent.Create();

        var arrow = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.TowerId));
        var control = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.ControlTowerId));
        var relay = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.UtilityTowerId));
        var runner = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.CreepId));
        var brute = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.BruteCreepId));
        var swarm = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.SwarmCreepId));

        Assert.True(arrow.Damage > control.Damage);
        Assert.True(control.AttackCooldownTicks > arrow.AttackCooldownTicks);
        Assert.True(relay.Cost.Amount > arrow.Cost.Amount);
        Assert.True(brute.MaxHealth > runner.MaxHealth);
        Assert.True(swarm.SpeedPerSecond > runner.SpeedPerSecond);
        Assert.True(brute.IncomeGain.Amount > runner.IncomeGain.Amount);
    }

    [Fact]
    public void Send_cooldown_creates_a_repeat_pressure_window()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var first = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);
        var immediate = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);
        for (var tick = 0; tick < 30; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var afterCooldown = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);

        Assert.True(first.Accepted);
        Assert.False(immediate.Accepted);
        Assert.Equal(CommandRejectionReason.CooldownActive, immediate.RejectionReason);
        Assert.True(afterCooldown.Accepted);
    }

    [Fact]
    public void Early_pressure_window_keeps_match_alive_before_first_income_tick()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 49; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        Assert.Equal(new SimulationTick(49), snapshot.Tick);
        Assert.True(snapshot.Players.Get(new PlayerId(1)).Lives.Amount > 0);
        Assert.True(snapshot.Players.Get(new PlayerId(1)).Income.Amount >= 11);
    }



    [Fact]
    public void Income_tick_emits_feedback_event()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        for (var tick = 0; tick < 50; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var events = simulation.DrainEvents();

        Assert.Contains(events, simulationEvent => simulationEvent is IncomeTickEvent income && income.PlayerId.Equals(new PlayerId(1)) && income.GoldAwarded.Amount == 10);
    }

    [Fact]
    public void Tower_attacks_emit_damage_feedback_events()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 1)).Accepted);
        Assert.True(simulation.QueueSend(new PlayerId(3), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 4; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var events = simulation.DrainEvents();

        Assert.Contains(events, simulationEvent => simulationEvent is CreepDamagedEvent);
    }

    [Fact]
    public void Leaked_creeps_continue_into_the_next_lane()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 17; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        var transferred = snapshot.Creeps.Single(creep =>
            creep.SenderId.Equals(new PlayerId(1)) &&
            creep.LaneId.Equals(new LaneId(3)) &&
            creep.Position.Equals(new GridPosition(3, 0)));
        var events = simulation.DrainEvents();

        Assert.Equal(new LaneId(3), transferred.LaneId);
        Assert.Equal(new GridPosition(3, 0), transferred.Position);
        Assert.Contains(events, simulationEvent => simulationEvent is LeakEvent leak && leak.DefenderId.Equals(new PlayerId(2)));
        Assert.Contains(events, simulationEvent => simulationEvent is CreepSpawnedEvent spawned && spawned.DefenderId.Equals(new PlayerId(3)));
    }

    [Fact]
    public void Sent_creeps_do_not_wrap_back_into_the_senders_own_lane()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 36; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        var events = simulation.DrainEvents();

        Assert.DoesNotContain(snapshot.Creeps, creep =>
            creep.SenderId.Equals(new PlayerId(1)) &&
            creep.LaneId.Equals(new LaneId(1)));
        Assert.DoesNotContain(events, simulationEvent =>
            simulationEvent is CreepSpawnedEvent spawned &&
            spawned.SenderId.Equals(new PlayerId(1)) &&
            spawned.DefenderId.Equals(new PlayerId(1)));
        Assert.DoesNotContain(events, simulationEvent =>
            simulationEvent is LeakEvent leak &&
            leak.SenderId.Equals(new PlayerId(1)) &&
            leak.DefenderId.Equals(new PlayerId(1)));
    }

    [Fact]
    public void Sent_creeps_cycle_through_active_opponent_lanes_until_killed()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 36; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        var events = simulation.DrainEvents();

        Assert.Contains(snapshot.Creeps, creep =>
            creep.SenderId.Equals(new PlayerId(1)) &&
            creep.LaneId.Equals(new LaneId(2)));
        Assert.DoesNotContain(snapshot.Creeps, creep =>
            creep.SenderId.Equals(new PlayerId(1)) &&
            creep.LaneId.Equals(new LaneId(1)));
        Assert.Contains(events, simulationEvent =>
            simulationEvent is LeakEvent leak &&
            leak.SenderId.Equals(new PlayerId(1)) &&
            leak.DefenderId.Equals(new PlayerId(3)));
        Assert.DoesNotContain(events, simulationEvent =>
            simulationEvent is CreepSpawnedEvent spawned &&
            spawned.SenderId.Equals(new PlayerId(1)) &&
            spawned.DefenderId.Equals(new PlayerId(1)));
        Assert.Contains(events, simulationEvent =>
            simulationEvent is CreepSpawnedEvent spawned &&
            spawned.SenderId.Equals(new PlayerId(1)) &&
            spawned.DefenderId.Equals(new PlayerId(2)));
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
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1)).Accepted);

        var sell = simulation.SellLastTower(new PlayerId(1));
        var snapshot = simulation.GetSnapshot();

        Assert.True(sell.Accepted);
        Assert.Empty(snapshot.Towers);
        Assert.Equal(87, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
    }
}
