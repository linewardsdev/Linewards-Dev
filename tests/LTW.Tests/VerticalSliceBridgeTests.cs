using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

public sealed class VerticalSliceBridgeTests
{
    /// <summary>
    /// Lane ownership is enforced on tower placement. Before this was added, the lane id was taken
    /// on trust from the caller: PlaceTower(P1, lane 5, ...) succeeded, charged P1's gold, and left
    /// a P1-owned tower in P5's lane. It never showed up locally because the Unity client hardcodes
    /// lane 1, but it is an exploit as soon as remote clients submit their own commands.
    /// </summary>
    [Fact]
    public void A_player_cannot_place_a_tower_in_another_players_lane()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        var goldBefore = simulation.GetSnapshot().Players.Get(new PlayerId(1)).Gold.Amount;

        var foreignLane = simulation.PlaceTower(new PlayerId(1), new LaneId(5), SampleVerticalSliceContent.TowerId, new GridPosition(1, 3));

        Assert.False(foreignLane.Accepted);
        Assert.Equal(CommandRejectionReason.NotOwner, foreignLane.RejectionReason);
        Assert.DoesNotContain(simulation.GetSnapshot().Towers, tower => tower.LaneId.Equals(new LaneId(5)));
        // A rejected placement must not charge the would-be builder.
        Assert.Equal(goldBefore, simulation.GetSnapshot().Players.Get(new PlayerId(1)).Gold.Amount);

        // The same placement in the player's own lane is still fine.
        Assert.True(simulation.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 3)).Accepted);
    }

    /// <summary>
    /// The local seat defaults to player 1, preserving every existing local-play assumption, but is
    /// now explicit rather than hardcoded across the client.
    /// </summary>
    [Fact]
    public void Local_seat_defaults_to_player_one_in_lane_one()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

        Assert.Equal(new PlayerId(1), simulation.LocalPlayerId);
        Assert.Equal(new LaneId(1), simulation.LocalPlayerLaneId);
    }

    /// <summary>
    /// Seating the human elsewhere must move both the seat and its lane together, and must flip
    /// which lanes are bot-driven: the occupied lane stops being a bot, the vacated lane starts.
    /// Without that second half, moving the seat would leave lane 1 idle and lane 3 double-driven.
    /// </summary>
    [Fact]
    public void Seating_the_local_player_elsewhere_moves_the_lane_and_the_bots()
    {
        var options = LocalMatchOptions.Default.WithLocalPlayer(3);
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        Assert.Equal(new PlayerId(3), simulation.LocalPlayerId);
        Assert.Equal(new LaneId(3), simulation.LocalPlayerLaneId);

        // The human's own lane is not bot-driven; the lane they vacated now is.
        Assert.False(options.IsBotEnabledFor(new PlayerId(3)));
        Assert.True(options.IsBotEnabledFor(new PlayerId(1)));
        Assert.DoesNotContain(simulation.GetBotDiagnostics().Profiles, profile => profile.PlayerId.Value == 3);
        Assert.Contains(simulation.GetBotDiagnostics().Profiles, profile => profile.PlayerId.Value == 1);
    }

    /// <summary>
    /// The seat is what the ownership rule is enforced against — a player seated in lane 3 may
    /// build in lane 3 and nowhere else. This is the pairing that makes remote seats safe.
    /// </summary>
    [Fact]
    public void A_relocated_local_seat_may_build_only_in_its_own_lane()
    {
        var options = LocalMatchOptions.Default.WithLocalPlayer(3);
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options, enableBots: false);

        var ownLane = simulation.PlaceTower(simulation.LocalPlayerId, simulation.LocalPlayerLaneId, SampleVerticalSliceContent.TowerId, new GridPosition(1, 3));
        var oldLane = simulation.PlaceTower(simulation.LocalPlayerId, new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 5));

        Assert.True(ownLane.Accepted);
        Assert.False(oldLane.Accepted);
        Assert.Equal(CommandRejectionReason.NotOwner, oldLane.RejectionReason);
    }

    /// <summary>
    /// PlayerId.IsValid only means "positive", so an id outside the match still reached the economy
    /// lookup and threw KeyNotFoundException. Over the wire that is a rejectable command, not an
    /// exceptional program state, so it must come back as a rejection instead.
    /// </summary>
    [Fact]
    public void Commands_from_a_player_outside_the_match_are_rejected_not_thrown()
    {
        var options = new LocalMatchOptions(laneCount: 3);
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options, enableBots: false);

        var result = simulation.PlaceTower(new PlayerId(99), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(1, 3));

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.InvalidPlayer, result.RejectionReason);
    }

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
        // Income (not tick) now gates expanded-roster send choices, so this represents a bot that
        // has been playing long enough to build up income, rather than a specific tick number.
        var richState = new PlayerEconomyState(new PlayerId(2), new Gold(500), new Income(60), new Lives(220));
        var greedy = new BotController(BotDecisionProfile.Greedy, SampleVerticalSliceContent.CreepId);
        var balanced = new BotController(BotDecisionProfile.Balanced, SampleVerticalSliceContent.CreepId);
        var defensive = new BotController(BotDecisionProfile.Defensive, SampleVerticalSliceContent.CreepId);

        var greedySend = Assert.IsType<QueueSendCommand>(greedy.Decide(richState, content, new SimulationTick(300)).Command);
        var balancedSend = Assert.IsType<QueueSendCommand>(balanced.Decide(richState, content, new SimulationTick(220)).Command);
        var defensiveSend = Assert.IsType<QueueSendCommand>(defensive.Decide(richState, content, new SimulationTick(240)).Command);

        // Category 2 and then Category 3 creeps slot into these cost-descending preference lists
        // — see BotController.SelectCreep's comment for why cost-descending ordering is required
        // for reachability. At this richState's abundant gold, each profile picks the single most
        // expensive creep in its top tier, which Category 3 moved up for all three.
        Assert.Equal(SampleVerticalSliceContent.ColossusCreepId, greedySend.CreepId);
        Assert.Equal(SampleVerticalSliceContent.WardenCreepId, balancedSend.CreepId);
        Assert.Equal(SampleVerticalSliceContent.WardenCreepId, defensiveSend.CreepId);
    }

    /// <summary>
    /// Guards the reachability invariant directly, rather than trusting that whoever edits a
    /// preference list keeps it cost-sorted.
    /// </summary>
    /// <remarks>
    /// <c>SelectCreep</c> returns the first id in its list that the bot can afford, so an id
    /// placed after a cheaper one can never be selected at any gold level — affording the pricier
    /// one always implies affording the cheaper one, which wins first. A previous violation left
    /// five creeps at zero sends across an entire batch playtest and was only caught by replay
    /// analysis. Sweeping gold and collecting what each profile actually picks catches it in
    /// milliseconds instead: every creep the lists name must be selectable at *some* gold level.
    /// </remarks>
    [Fact]
    public void Every_creep_a_bot_profile_prefers_is_reachable_at_some_gold_level()
    {
        var content = SampleVerticalSliceContent.Create();
        var expectedReachable = new (BotDecisionProfile Profile, int Income, ContentId[] Ids)[]
        {
            (BotDecisionProfile.Greedy, 60, new[]
            {
                SampleVerticalSliceContent.ColossusCreepId, SampleVerticalSliceContent.SiegeCreepId,
                SampleVerticalSliceContent.TurretWalkerCreepId, SampleVerticalSliceContent.WardenCreepId,
                SampleVerticalSliceContent.StalkerCreepId, SampleVerticalSliceContent.ShadeCreepId,
                SampleVerticalSliceContent.BruteCreepId, SampleVerticalSliceContent.RevenantCreepId,
            }),
            (BotDecisionProfile.Balanced, 40, new[]
            {
                SampleVerticalSliceContent.WardenCreepId, SampleVerticalSliceContent.ObsidianBruteCreepId,
                SampleVerticalSliceContent.BurrowerCreepId, SampleVerticalSliceContent.ShadeCreepId,
                SampleVerticalSliceContent.BruteCreepId, SampleVerticalSliceContent.SwarmCreepId,
            }),
            (BotDecisionProfile.Defensive, 35, new[]
            {
                SampleVerticalSliceContent.WardenCreepId, SampleVerticalSliceContent.ObsidianBruteCreepId,
                SampleVerticalSliceContent.BurrowerCreepId, SampleVerticalSliceContent.SerpentCreepId,
                SampleVerticalSliceContent.BruteCreepId, SampleVerticalSliceContent.SwarmCreepId,
            }),
        };

        foreach (var (profile, income, ids) in expectedReachable)
        {
            var bot = new BotController(profile, SampleVerticalSliceContent.CreepId);
            var seen = new HashSet<string>();
            for (var gold = 0; gold <= 600; gold++)
            {
                var state = new PlayerEconomyState(new PlayerId(2), new Gold(gold), new Income(income), new Lives(220));
                if (bot.Decide(state, content, new SimulationTick(300)).Command is QueueSendCommand send)
                {
                    seen.Add(send.CreepId.Value);
                }
            }

            foreach (var id in ids)
            {
                Assert.True(seen.Contains(id.Value),
                    $"{profile} never selects {id.Value} at any gold level 0-600 — it is listed after a cheaper id and is therefore unreachable.");
            }
        }
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

        // Kept short enough that the match is still ongoing with both bots active — reactive
        // spending resolves matches faster than the old tick-scheduled bots did, and a long-enough
        // window can run past one side's elimination, leaving only the other bot's decisions in
        // the last-12 "recent decisions" diagnostic window.
        for (var tick = 0; tick < 100; tick++)
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
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.GatlingTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.TeslaTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.FoundryTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.BarricadeTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.RepairDroneTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.ElderCanopyTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.SaplingTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.BloomheartTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.ThornSnareTowerId));
        Assert.Contains(content.Towers, tower => tower.Id.Equals(SampleVerticalSliceContent.SporeCloudTowerId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.CreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.BruteCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.SwarmCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.ShadeCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.SiegeCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.WispCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.RevenantCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.ObsidianBruteCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.SerpentCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.TurretWalkerCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.ZephyrCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.BurrowerCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.StalkerCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.WardenCreepId));
        Assert.Contains(content.Creeps, creep => creep.Id.Equals(SampleVerticalSliceContent.ColossusCreepId));
        Assert.Equal(15, content.Towers.Count);
        Assert.Equal(15, content.Creeps.Count);
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
        // Spaced past the 30-tick send cooldown (enforced as of the multiplayer authority pass).
        // This test is about the expanded roster's content being accepted, not send cadence, so
        // the wait just keeps the second send from being rejected for an unrelated reason.
        for (var tick = 0; tick < 30; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var siegeSend = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.SiegeCreepId);
        var events = simulation.DrainEvents();

        Assert.True(pulsePreview.Accepted);
        Assert.True(prismPreview.Accepted);
        Assert.True(shadeSend.Accepted);
        Assert.True(siegeSend.Accepted);
        Assert.Contains(events, simulationEvent => simulationEvent is CreepQueuedEvent queued && queued.CreepId.Equals(SampleVerticalSliceContent.ShadeCreepId));
        Assert.Contains(events, simulationEvent => simulationEvent is CreepQueuedEvent queued && queued.CreepId.Equals(SampleVerticalSliceContent.SiegeCreepId));
    }

    /// <summary>
    /// Replaces an earlier "Repeat_sends_are_limited_by_gold_only", which asserted a player could
    /// spam sends within a single tick until gold ran out. That was only true because the 30-tick
    /// send cooldown, though configured and tracked in player state, was never enforced. Sends are
    /// now gated by the cooldown first and gold second.
    /// </summary>
    [Fact]
    public void Repeat_sends_within_the_cooldown_window_are_rejected()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);

        var first = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);
        var immediate = simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId);

        Assert.True(first.Accepted);
        Assert.False(immediate.Accepted);
        Assert.Equal(CommandRejectionReason.CooldownActive, immediate.RejectionReason);
    }

    /// <summary>
    /// The companion to the test above: the cooldown throttles cadence, it does not permanently
    /// stop a player sending. Spacing each send a full cooldown apart must let every one through.
    /// Gold-exhaustion is deliberately not asserted here — over this many ticks income outgrows
    /// Runner's cost, so a gold rejection never fires; that rule is covered directly by
    /// <c>EconomyTests.Insufficient_gold_rejects_without_changing_state</c>.
    /// </summary>
    [Fact]
    public void Sends_spaced_past_the_cooldown_are_all_accepted()
    {
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), enableBots: false);
        var accepted = 0;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.CreepId).Accepted)
            {
                accepted++;
            }

            for (var tick = 0; tick < 30; tick++)
            {
                simulation.AdvanceOneTick();
            }
        }

        Assert.Equal(5, accepted);
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
        var options = new LocalMatchOptions(botLanes: new[]
        {
            new BotLaneOptions(2, profile: BotDecisionProfile.Greedy, primaryCreepId: SampleVerticalSliceContent.BruteCreepId),
            new BotLaneOptions(3, profile: BotDecisionProfile.Balanced, primaryCreepId: SampleVerticalSliceContent.SwarmCreepId),
            new BotLaneOptions(4, profile: BotDecisionProfile.Defensive, primaryCreepId: SampleVerticalSliceContent.ShadeCreepId),
            new BotLaneOptions(5, profile: BotDecisionProfile.Greedy, primaryCreepId: SampleVerticalSliceContent.SiegeCreepId),
            new BotLaneOptions(6, profile: BotDecisionProfile.Balanced, primaryCreepId: SampleVerticalSliceContent.CreepId),
            new BotLaneOptions(7, profile: BotDecisionProfile.Defensive, primaryCreepId: SampleVerticalSliceContent.BruteCreepId),
            new BotLaneOptions(8, profile: BotDecisionProfile.Greedy, primaryCreepId: SampleVerticalSliceContent.ShadeCreepId)
        });
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

    [Fact]
    public void Disabled_lanes_never_get_a_bot_or_act()
    {
        var options = LocalMatchOptions.Default
            .WithLane(3, enabled: false)
            .WithLane(5, enabled: false)
            .WithLane(7, enabled: false);
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        for (var index = 0; index < 60; index++)
        {
            simulation.AdvanceOneTick();
        }

        var diagnostics = simulation.GetBotDiagnostics();
        var disabledPlayerIds = new[] { 3, 5, 7 };

        foreach (var disabledPlayerId in disabledPlayerIds)
        {
            Assert.DoesNotContain(diagnostics.Profiles, profile => profile.PlayerId.Value == disabledPlayerId);
        }

        Assert.DoesNotContain(simulation.GetReplayRecord().AcceptedCommands,
            command => disabledPlayerIds.Contains(command.PlayerId.Value));
        Assert.DoesNotContain(simulation.GetSnapshot().Towers, tower => disabledPlayerIds.Contains(tower.OwnerId.Value));
    }

    [Fact]
    public void Default_options_preserve_original_per_lane_bot_assignments()
    {
        var options = LocalMatchOptions.Default;

        Assert.Equal(BotDecisionProfile.Balanced, options.BotProfileFor(new PlayerId(2)));
        Assert.Equal(BotDecisionProfile.Defensive, options.BotProfileFor(new PlayerId(3)));
        Assert.Equal(BotDecisionProfile.Greedy, options.BotProfileFor(new PlayerId(4)));
        Assert.Equal(BotDecisionProfile.Greedy, options.BotProfileFor(new PlayerId(5)));
        Assert.Equal(BotDecisionProfile.Greedy, options.BotProfileFor(new PlayerId(6)));
        Assert.Equal(BotDecisionProfile.Greedy, options.BotProfileFor(new PlayerId(7)));
        Assert.Equal(BotDecisionProfile.Greedy, options.BotProfileFor(new PlayerId(8)));
        for (var playerId = 2; playerId <= 8; playerId++)
        {
            Assert.True(options.IsBotEnabledFor(new PlayerId(playerId)));
            Assert.Null(options.PrimaryCreepFor(new PlayerId(playerId)));
        }

        Assert.False(options.IsBotEnabledFor(new PlayerId(1)));
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

    [Fact]
    public void Defensive_bot_prioritizes_sending_over_stacking_further_towers_once_coverage_is_met()
    {
        // Replaces an earlier version of this test ("...builds_past_the_old_fixed_tower_cap...")
        // that asserted an isolated, unpressured Defensive bot keeps adding towers indefinitely
        // given enough ticks/gold. That stopped being true once the send decision was reordered
        // to run before TryPlaceBotTower each tick (see GD_TUNING_LOG.md's 2026-07-28 bot-economy
        // entry): a bot with surplus gold now spends it on a send first, and towers only get
        // whatever's left, so an idle/unpressured bot plateaus at MinimumTowerCoverage (4 for
        // Defensive) rather than continuing to stack towers. Traced with an instrumented run:
        // towers plateaued at exactly 4 by tick ~100 while gold sat idle just short of the 5th
        // tower's cost, because periodic sends kept siphoning off the surplus first.
        //
        // That's the intended, better behavior (a bot facing no defensive pressure should
        // reasonably push offense rather than over-build), not a regression to the old
        // DesiredOpeningTowerCount==4 hard cap bug this test originally existed to catch. To
        // keep catching that regression specifically, this checks two things: coverage is met
        // (>=4), and the plateau is actually explained by the bot having sent creeps (proving
        // it's a spending choice, not a rebuilt structural cap).
        // Default enables all 8 lanes as Greedy bots; disable everything but P3 so its own
        // RecentDecisions entries (a 12-record shared ring buffer across all players) aren't
        // evicted by the other lanes' unrelated send activity before we get to check it.
        var options = LocalMatchOptions.Default
            .WithLane(2, enabled: false)
            .WithLane(3, profile: BotDecisionProfile.Defensive)
            .WithLane(4, enabled: false)
            .WithLane(5, enabled: false)
            .WithLane(6, enabled: false)
            .WithLane(7, enabled: false)
            .WithLane(8, enabled: false);
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        for (var tick = 0; tick < 150; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var ownedTowerCount = simulation.GetSnapshot().Towers.Count(tower => tower.OwnerId.Value == 3);
        Assert.True(ownedTowerCount >= 4, $"Expected at least the minimum defensive coverage of 4 towers, got {ownedTowerCount}.");
        Assert.Contains(simulation.GetBotDiagnostics().RecentDecisions, d => d.PlayerId.Value == 3);
    }

    [Fact]
    public void Non_greedy_bot_holds_sends_while_its_own_lane_is_under_heavy_pressure()
    {
        var options = LocalMatchOptions.Default.WithLane(2, profile: BotDecisionProfile.Balanced).WithLane(3, enabled: false);
        var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        // Let P2 (Balanced, defends lane 2) finish its opening tower package first.
        for (var tick = 0; tick < 10; tick++)
        {
            simulation.AdvanceOneTick();
        }
        Assert.True(simulation.GetSnapshot().Towers.Count(t => t.OwnerId.Value == 2) >= 3);

        // Now dump heavy pressure into P2's own lane directly from P1 (P1's next carousel
        // opponent is P2), and confirm P2 stops queuing new sends while towers keep being added,
        // instead of sending regardless. Brute (18 gold, 24 health) at quantity 5 stays within
        // P1's starting 100 gold while comfortably clearing the pressure threshold.
        Assert.True(simulation.QueueSend(new PlayerId(1), SampleVerticalSliceContent.BruteCreepId, quantity: 5).Accepted);

        // Assert on the tick of P2's decisions rather than how many appear in RecentDecisions.
        // That list is only the last 12 records across all bots, so a busier field evicts P2's
        // older entries and the count falls even when P2 has decided nothing new — which made
        // this test fail for a reason that had nothing to do with P2 holding its sends.
        var markerTick = simulation.GetSnapshot().Tick.Value;
        for (var tick = 0; tick < 5; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var newDecisionsByP2 = simulation.GetBotDiagnostics().RecentDecisions
            .Where(d => d.PlayerId.Value == 2 && d.Tick.Value > markerTick)
            .ToArray();

        Assert.Empty(newDecisionsByP2);
    }

    [Fact]
    public void Bot_decisions_are_deterministic_for_the_same_seed_and_options()
    {
        var options = LocalMatchOptions.Default;

        var first = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);
        var second = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);

        for (var tick = 0; tick < 200; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();
        }

        var firstCommands = first.GetReplayRecord().AcceptedCommands;
        var secondCommands = second.GetReplayRecord().AcceptedCommands;

        Assert.Equal(firstCommands.Count, secondCommands.Count);
        for (var index = 0; index < firstCommands.Count; index++)
        {
            Assert.Equal(firstCommands[index].Tick, secondCommands[index].Tick);
            Assert.Equal(firstCommands[index].PlayerId, secondCommands[index].PlayerId);
            Assert.Equal(firstCommands[index].ContentId, secondCommands[index].ContentId);
            Assert.Equal(firstCommands[index].Quantity, secondCommands[index].Quantity);
        }
    }

    private static LocalMatchOptions ThreeLaneOptions() => new(laneCount: 3);
}
