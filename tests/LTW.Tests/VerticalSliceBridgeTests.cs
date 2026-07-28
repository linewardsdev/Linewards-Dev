using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

public sealed class VerticalSliceBridgeTests
{
    [Fact]
    public void Local_vertical_slice_places_tower_sends_creep_and_advances_to_expected_state()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

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
        Assert.Equal(76, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(11, snapshot.Players.Get(new PlayerId(1)).Income.Amount);
        Assert.Contains(events, simulationEvent => simulationEvent is TowerPlacedEvent);
        Assert.Contains(events, simulationEvent => simulationEvent is CreepSpawnedEvent);
    }

    [Fact]
    public void Bridge_rejects_invalid_path_or_affordability_without_duplicate_rules_in_unity()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

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
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.UtilityTowerId, new GridPosition(3, 1)).Accepted);

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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions());

        for (var tick = 0; tick < 60; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        Assert.Contains(snapshot.Towers, tower => tower.OwnerId.Equals(new PlayerId(2)) && tower.TowerId.Equals(SampleVerticalSliceContent.TowerId));
        Assert.Contains(snapshot.Towers, tower => tower.OwnerId.Equals(new PlayerId(3)) && tower.TowerId.Equals(SampleVerticalSliceContent.ControlTowerId));
        Assert.Contains(snapshot.Towers, tower => tower.OwnerId.Equals(new PlayerId(2)) && tower.TowerId.Equals(SampleVerticalSliceContent.PulseTowerId));
        Assert.True(snapshot.Towers.Count(tower => tower.OwnerId.Equals(new PlayerId(2))) >= 3);
        Assert.True(snapshot.Towers.Count(tower => tower.OwnerId.Equals(new PlayerId(3))) >= 3);
    }

    [Fact]
    public void Default_eight_lane_match_stays_clean_until_started()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var snapshot = simulation.GetSnapshot();
        var diagnostics = simulation.GetBotDiagnostics();
        var events = simulation.DrainEvents();

        Assert.Equal(8, snapshot.Players.Players.Count);
        Assert.Empty(snapshot.Towers);
        Assert.Empty(snapshot.Creeps);
        Assert.Empty(diagnostics.RecentDecisions);
        Assert.DoesNotContain(events, simulationEvent => simulationEvent is TowerPlacedEvent or CreepQueuedEvent or CreepSpawnedEvent);
    }

    [Fact]
    public void Eight_lane_bots_build_and_send_from_lanes_two_through_eight_at_match_start()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
        simulation.StartMatch();

        var snapshot = simulation.GetSnapshot();
        var diagnostics = simulation.GetBotDiagnostics();
        var events = simulation.DrainEvents();

        for (var playerId = 2; playerId <= 8; playerId++)
        {
            var expectedDefenderId = playerId == 8 ? 1 : playerId + 1;
            Assert.Contains(snapshot.Towers, tower => tower.OwnerId.Equals(new PlayerId(playerId)) && tower.LaneId.Equals(new LaneId(playerId)));
            Assert.Contains(snapshot.Creeps, creep => creep.SenderId.Equals(new PlayerId(playerId)) && creep.LaneId.Equals(new LaneId(expectedDefenderId)));
            Assert.Contains(diagnostics.RecentDecisions, decision => decision.PlayerId.Equals(new PlayerId(playerId)));
            Assert.Contains(events, simulationEvent =>
                simulationEvent is CreepQueuedEvent queued &&
                queued.SenderId.Equals(new PlayerId(playerId)) &&
                queued.DefenderId.Equals(new PlayerId(expectedDefenderId)));
        }
    }

    [Fact]
    public void Eight_lane_send_targets_follow_carousel_topology()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

        for (var playerId = 1; playerId <= 8; playerId++)
        {
            Assert.True(simulation.QueueSend(new PlayerId(playerId), SampleVerticalSliceContent.CreepId).Accepted);
        }

        var snapshot = simulation.GetSnapshot();
        var events = simulation.DrainEvents();

        for (var playerId = 1; playerId <= 8; playerId++)
        {
            var expectedDefenderId = playerId == 8 ? 1 : playerId + 1;
            Assert.Contains(snapshot.Creeps, creep =>
                creep.SenderId.Equals(new PlayerId(playerId)) &&
                creep.LaneId.Equals(new LaneId(expectedDefenderId)) &&
                creep.Position.Equals(new GridPosition(3, 0)));
            Assert.Contains(events, simulationEvent =>
                simulationEvent is CreepSpawnedEvent spawned &&
                spawned.SenderId.Equals(new PlayerId(playerId)) &&
                spawned.DefenderId.Equals(new PlayerId(expectedDefenderId)));
        }
    }

    [Fact]
    public void Bot_profiles_choose_expanded_roster_sends_when_available()
    {
        var content = SampleVerticalSliceContent.Create();
        var richState = new PlayerEconomyState(new PlayerId(2), new Gold(500), new Income(10), new Lives(220));
        var greedy = new BotController(BotDecisionProfile.Greedy, SampleVerticalSliceContent.CreepId);
        var balanced = new BotController(BotDecisionProfile.Balanced, SampleVerticalSliceContent.CreepId);
        var defensive = new BotController(BotDecisionProfile.Defensive, SampleVerticalSliceContent.CreepId);

        var greedySend = Assert.IsType<QueueSendCommand>(greedy.Decide(richState, content, new SimulationTick(300)).Command);
        var balancedSend = Assert.IsType<QueueSendCommand>(balanced.Decide(richState, content, new SimulationTick(220)).Command);
        var defensiveSend = Assert.IsType<QueueSendCommand>(defensive.Decide(richState, content, new SimulationTick(240)).Command);

        Assert.Equal(SampleVerticalSliceContent.SiegeCreepId, greedySend.CreepId);
        Assert.Equal(SampleVerticalSliceContent.ShadeCreepId, balancedSend.CreepId);
        Assert.Equal(SampleVerticalSliceContent.BruteCreepId, defensiveSend.CreepId);
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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions());

        var initial = simulation.GetBotDiagnostics();
        Assert.Contains(initial.Profiles, profile => profile.PlayerId.Equals(new PlayerId(2)) && profile.Profile == LTW.Simulation.Bots.BotDecisionProfile.Balanced);
        Assert.Contains(initial.Profiles, profile => profile.PlayerId.Equals(new PlayerId(3)) && profile.Profile == LTW.Simulation.Bots.BotDecisionProfile.Defensive);

        for (var tick = 0; tick < 500; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var diagnostics = simulation.GetBotDiagnostics();
        Assert.Contains(diagnostics.RecentDecisions, decision => decision.PlayerId.Equals(new PlayerId(2)) && decision.Quantity >= 1);
        Assert.Contains(diagnostics.RecentDecisions, decision => decision.PlayerId.Equals(new PlayerId(3)) && decision.Quantity == 1);
    }

    [Fact]
    public void Bridge_reset_clears_bot_decision_diagnostics()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions());
        for (var tick = 0; tick < 500; tick++)
        {
            simulation.AdvanceOneTick();
        }

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
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.PulseTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.PrismTowerId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.CreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.BruteCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.SwarmCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.ShadeCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.SiegeCreepId));
        Assert.Equal(5, content.Towers.Count);
        Assert.Equal(5, content.Creeps.Count);
    }

    [Fact]
    public void Sample_content_has_distinct_tower_and_send_pressure_profiles()
    {
        var content = SampleVerticalSliceContent.Create();

        var arrow = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.TowerId));
        var control = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.ControlTowerId));
        var relay = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.UtilityTowerId));
        var pulse = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.PulseTowerId));
        var prism = content.Towers.Single(tower => tower.Id.Equals(SampleVerticalSliceContent.PrismTowerId));
        var runner = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.CreepId));
        var brute = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.BruteCreepId));
        var swarm = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.SwarmCreepId));
        var shade = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.ShadeCreepId));
        var siege = content.Creeps.Single(creep => creep.Id.Equals(SampleVerticalSliceContent.SiegeCreepId));

        Assert.Equal(2, arrow.Damage);
        Assert.Equal(2, arrow.AttackCooldownTicks);
        Assert.Equal(2, relay.Damage);
        Assert.Equal(2, relay.RangeCells);
        Assert.Equal(4, relay.AttackCooldownTicks);
        Assert.True(arrow.AttackCooldownTicks < control.AttackCooldownTicks);
        Assert.True(control.AttackCooldownTicks > arrow.AttackCooldownTicks);
        Assert.True(relay.Cost.Amount > arrow.Cost.Amount);
        Assert.True(pulse.Damage > control.Damage);
        Assert.True(prism.RangeCells > arrow.RangeCells);
        Assert.True(prism.Cost.Amount > pulse.Cost.Amount);
        Assert.True(brute.MaxHealth > runner.MaxHealth);
        Assert.True(swarm.SpeedPerSecond > runner.SpeedPerSecond);
        Assert.True(brute.IncomeGain.Amount > runner.IncomeGain.Amount);
        Assert.True(shade.SpeedPerSecond > brute.SpeedPerSecond);
        Assert.True(shade.IncomeGain.Amount > brute.IncomeGain.Amount);
        Assert.True(siege.MaxHealth > brute.MaxHealth);
        Assert.True(siege.Cost.Amount > shade.Cost.Amount);
    }

    [Fact]
    public void Relay_ward_generates_signal_gold_when_it_hits_creeps()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        var playerOne = new PlayerId(1);

        Assert.True(simulation.PlaceTower(playerOne, new LaneId(1), SampleVerticalSliceContent.UtilityTowerId, new GridPosition(1, 1)).Accepted);
        var goldBeforeCombat = simulation.GetSnapshot().Players.Get(playerOne).Gold.Amount;

        Assert.True(simulation.QueueSend(new PlayerId(8), SampleVerticalSliceContent.CreepId).Accepted);
        simulation.AdvanceOneTick();
        var events = simulation.DrainEvents();

        Assert.Contains(events, simulationEvent => simulationEvent is CreepDamagedEvent damaged && damaged.DefenderId.Equals(playerOne));
        Assert.Equal(goldBeforeCombat + 1, simulation.GetSnapshot().Players.Get(playerOne).Gold.Amount);
    }

    [Fact]
    public void Expanded_roster_content_accepts_new_tower_and_creep_commands()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

        var pulsePreview = simulation.PreviewPlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.PulseTowerId, new GridPosition(1, 1));
        var prismPreview = simulation.PreviewPlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.PrismTowerId, new GridPosition(5, 1));
        var shadeSend = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.ShadeCreepId);
        var siegeSend = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.SiegeCreepId);
        var events = simulation.DrainEvents();

        Assert.True(pulsePreview.Accepted);
        Assert.True(prismPreview.Accepted);
        Assert.True(shadeSend.Accepted);
        Assert.True(siegeSend.Accepted);
        Assert.Contains(events, simulationEvent => simulationEvent is CreepQueuedEvent queued && queued.CreepId.Equals(SampleVerticalSliceContent.ShadeCreepId));
        Assert.Contains(events, simulationEvent => simulationEvent is CreepQueuedEvent queued && queued.CreepId.Equals(SampleVerticalSliceContent.SiegeCreepId));
    }

    [Fact]
    public void Repeat_sends_are_limited_by_gold_only()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

        var first = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);
        var immediate = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);
        for (var send = 0; send < 8; send++)
        {
            Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);
        }

        var noGold = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);

        Assert.True(first.Accepted);
        Assert.True(immediate.Accepted);
        Assert.False(noGold.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, noGold.RejectionReason);
    }

    [Fact]
    public void Early_pressure_window_keeps_match_alive_before_first_income_tick()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 15; tick++)
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
    public void Leaked_creeps_keep_current_health_when_entering_next_lane()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
        Assert.True(simulation.PlaceTower(new PlayerId(2), new LaneId(2), SampleVerticalSliceContent.UtilityTowerId, new GridPosition(2, 8)).Accepted);
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 15; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        var transferred = snapshot.Creeps.Single(creep =>
            creep.SenderId.Equals(new PlayerId(1)) &&
            creep.LaneId.Equals(new LaneId(3)) &&
            creep.Position.Equals(new GridPosition(3, 0)));

        Assert.Equal(8, transferred.Health);
    }

    [Fact]
    public void Eight_lane_leaked_creeps_flow_across_expanded_opponent_lanes()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted);

        for (var tick = 0; tick < 15; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.Contains(simulation.GetSnapshot().Creeps, creep =>
            creep.SenderId.Equals(new PlayerId(1)) &&
            creep.LaneId.Equals(new LaneId(3)) &&
            creep.Position.Equals(new GridPosition(3, 0)));

        for (var tick = 0; tick < 15; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.GetSnapshot();
        var events = simulation.DrainEvents();
        Assert.Contains(snapshot.Creeps, creep =>
            creep.SenderId.Equals(new PlayerId(1)) &&
            creep.LaneId.Equals(new LaneId(4)) &&
            creep.Position.Equals(new GridPosition(3, 0)));
        Assert.Contains(events, simulationEvent => simulationEvent is LeakEvent leak && leak.SenderId.Equals(new PlayerId(1)) && leak.DefenderId.Equals(new PlayerId(3)));
        Assert.Contains(events, simulationEvent => simulationEvent is CreepSpawnedEvent spawned && spawned.SenderId.Equals(new PlayerId(1)) && spawned.DefenderId.Equals(new PlayerId(4)));
    }

    [Fact]
    public void Sent_creeps_do_not_wrap_back_into_the_senders_own_lane()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
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
    public void Eight_lane_sends_never_reenter_their_senders_home_lane()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        for (var playerId = 1; playerId <= 8; playerId++)
        {
            Assert.True(simulation.QueueSend(new PlayerId(playerId), SampleVerticalSliceContent.CreepId).Accepted);
        }

        AssertNoSenderHomeLaneCreeps(simulation.GetSnapshot());
        AssertNoSenderHomeLaneEvents(simulation.DrainEvents());

        for (var tick = 0; tick < 120; tick++)
        {
            simulation.AdvanceOneTick();
            AssertNoSenderHomeLaneCreeps(simulation.GetSnapshot());
            AssertNoSenderHomeLaneEvents(simulation.DrainEvents());
        }
    }

    [Fact]
    public void Sent_creeps_cycle_through_active_opponent_lanes_until_killed()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
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
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 1)).Accepted);

        var sell = simulation.SellLastTower(new PlayerId(1));
        var snapshot = simulation.GetSnapshot();

        Assert.True(sell.Accepted);
        Assert.Empty(snapshot.Towers);
        Assert.Equal(93, snapshot.Players.Get(new PlayerId(1)).Gold.Amount);
    }

    [Fact]
    public void Default_local_vertical_slice_uses_eight_player_lanes()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());

        var snapshot = simulation.GetSnapshot();
        var diagnostics = simulation.GetBotDiagnostics();

        Assert.Equal(8, snapshot.Players.Players.Count);
        Assert.Equal(7, diagnostics.Profiles.Count);
        Assert.True(simulation.QueueSend(new PlayerId(8), SampleVerticalSliceContent.CreepId).Accepted);
        Assert.Contains(simulation.DrainEvents(), simulationEvent =>
            simulationEvent is CreepQueuedEvent queued &&
            queued.SenderId.Equals(new PlayerId(8)) &&
            queued.DefenderId.Equals(new PlayerId(1)));
    }

    [Fact]
    public void Eight_lane_match_options_configure_each_bot_lane()
    {
        var options = new LocalMatchOptions(
            player2Profile: BotDecisionProfile.Greedy,
            player3Profile: BotDecisionProfile.Balanced,
            player4Profile: BotDecisionProfile.Defensive,
            player5Profile: BotDecisionProfile.Greedy,
            player6Profile: BotDecisionProfile.Balanced,
            player7Profile: BotDecisionProfile.Defensive,
            player8Profile: BotDecisionProfile.Greedy,
            player2PrimaryCreepId: SampleVerticalSliceContent.BruteCreepId,
            player3PrimaryCreepId: SampleVerticalSliceContent.SwarmCreepId,
            player4PrimaryCreepId: SampleVerticalSliceContent.ShadeCreepId,
            player5PrimaryCreepId: SampleVerticalSliceContent.SiegeCreepId,
            player6PrimaryCreepId: SampleVerticalSliceContent.CreepId,
            player7PrimaryCreepId: SampleVerticalSliceContent.BruteCreepId,
            player8PrimaryCreepId: SampleVerticalSliceContent.ShadeCreepId);
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        var diagnostics = simulation.GetBotDiagnostics();

        Assert.Contains(diagnostics.Profiles, profile => profile.PlayerId.Equals(new PlayerId(2)) && profile.Profile == BotDecisionProfile.Greedy && profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.BruteCreepId));
        Assert.Contains(diagnostics.Profiles, profile => profile.PlayerId.Equals(new PlayerId(3)) && profile.Profile == BotDecisionProfile.Balanced && profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.SwarmCreepId));
        Assert.Contains(diagnostics.Profiles, profile => profile.PlayerId.Equals(new PlayerId(4)) && profile.Profile == BotDecisionProfile.Defensive && profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.ShadeCreepId));
        Assert.Contains(diagnostics.Profiles, profile => profile.PlayerId.Equals(new PlayerId(5)) && profile.Profile == BotDecisionProfile.Greedy && profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.SiegeCreepId));
        Assert.Contains(diagnostics.Profiles, profile => profile.PlayerId.Equals(new PlayerId(6)) && profile.Profile == BotDecisionProfile.Balanced && profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.CreepId));
        Assert.Contains(diagnostics.Profiles, profile => profile.PlayerId.Equals(new PlayerId(7)) && profile.Profile == BotDecisionProfile.Defensive && profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.BruteCreepId));
        Assert.Contains(diagnostics.Profiles, profile => profile.PlayerId.Equals(new PlayerId(8)) && profile.Profile == BotDecisionProfile.Greedy && profile.PrimaryCreepId.Equals(SampleVerticalSliceContent.ShadeCreepId));
    }

    private static void AssertNoSenderHomeLaneCreeps(VerticalSliceSnapshot snapshot)
    {
        Assert.DoesNotContain(snapshot.Creeps, creep => creep.LaneId.Value == creep.SenderId.Value);
    }

    private static void AssertNoSenderHomeLaneEvents(IReadOnlyList<ISimulationEvent> events)
    {
        Assert.DoesNotContain(events, simulationEvent =>
            simulationEvent is CreepSpawnedEvent spawned &&
            spawned.SenderId.Equals(spawned.DefenderId));
        Assert.DoesNotContain(events, simulationEvent =>
            simulationEvent is LeakEvent leak &&
            leak.SenderId.Equals(leak.DefenderId));
    }

    private static LocalMatchOptions ThreeLaneOptions() => new(laneCount: 3);
}
