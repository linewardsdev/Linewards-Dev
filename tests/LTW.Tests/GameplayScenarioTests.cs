using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Bridge;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit.Abstractions;

namespace LTW.Tests;

public sealed class GameplayScenarioTests
{
    private readonly ITestOutputHelper output;

    public GameplayScenarioTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Low_pressure_scenario_records_stable_opening_defense()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 8)).Accepted);
        Assert.True(slice.QueueSend(new PlayerId(3), SampleVerticalSliceContent.CreepId).Accepted);

        RunTicks(slice, 80);

        var evidence = CaptureEvidence(slice);

        output.WriteLine(evidence.ToString());
        Assert.True(evidence.AcceptedCommands >= 1);
        // Lowered from 219 alongside the tower cost/damage rebalance (cheaper, weaker towers):
        // one extra Runner hit lands in this fixed 80-tick window before defense catches up.
        Assert.True(evidence.PlayerOneLives >= 218);
        Assert.True(evidence.DamageEvents >= 1);
        // Lowered from 658 for the same reason as the PlayerOneLives threshold above.
        Assert.True(evidence.TotalLives >= 656);
        Assert.Null(slice.MatchSummary);
    }

    [Fact]
    public void Normal_pressure_scenario_records_income_and_active_combat()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 8)).Accepted);
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.ControlTowerId, new GridPosition(4, 8)).Accepted);
        Assert.True(slice.QueueSend(new PlayerId(3), SampleVerticalSliceContent.CreepId).Accepted);
        // Spaced past the 30-tick send cooldown (enforced as of the multiplayer authority pass).
        // This scenario is about income accruing alongside active combat, not send cadence; the
        // wait is folded into the run below so total elapsed ticks stay in the same ballpark.
        RunTicks(slice, 30);
        Assert.True(slice.QueueSend(new PlayerId(3), SampleVerticalSliceContent.BruteCreepId).Accepted);

        RunTicks(slice, 120);

        var evidence = CaptureEvidence(slice);

        output.WriteLine(evidence.ToString());
        Assert.True(evidence.AcceptedCommands >= 2);
        Assert.True(evidence.TotalIncome > 30);
        Assert.True(evidence.DamageEvents >= 1);
        Assert.True(evidence.LeakEvents >= 1);
        Assert.Null(slice.MatchSummary);
    }

    [Fact]
    public void Heavy_pressure_scenario_records_escalation_without_hidden_bot_advantages()
    {
        var slice = CreateSlice(
            BotDecisionProfile.Greedy,
            BotDecisionProfile.Greedy,
            SampleVerticalSliceContent.SiegeCreepId,
            SampleVerticalSliceContent.ShadeCreepId);

        RunTicks(slice, 360);

        var evidence = CaptureEvidence(slice);

        output.WriteLine(evidence.ToString());
        Assert.True(evidence.AcceptedCommands >= 10);
        Assert.True(evidence.MultiQuantityCommands >= 1);
        Assert.True(evidence.LeakEvents >= 1);

        // Lives are CONSERVED across a leak, not destroyed: a leak moves one from defender to
        // sender. This used to assert the total had dropped below the 660 the three players start
        // with, which encoded the older rule where a leak simply erased a life. Under stealing the
        // total can only fall when a leak lands for an already-eliminated sender, who is credited
        // nothing — so the bound is now one-sided, and the redistribution is what gets asserted.
        Assert.True(evidence.TotalLives <= 660);
        var lives = slice.GetSnapshot().Players.Players.Select(player => player.Lives.Amount).ToList();
        Assert.True(lives.Max() > lives.Min(), $"leaks should have redistributed lives, but all players hold {lives.Max()}");
        Assert.All(slice.GetSnapshot().Players.Players, player =>
        {
            Assert.True(player.Gold.Amount >= 0);
            Assert.True(player.Income.Amount >= 0);
            Assert.True(player.Lives.Amount >= 0);
        });
    }

    [Fact]
    public void Mixed_pressure_scenario_records_distinct_send_roles_and_defensive_response()
    {
        var slice = CreateSlice(
            BotDecisionProfile.Greedy,
            BotDecisionProfile.Balanced,
            SampleVerticalSliceContent.SiegeCreepId,
            SampleVerticalSliceContent.SwarmCreepId);

        RunTicks(slice, 420);

        var evidence = CaptureEvidence(slice);
        var replay = slice.GetReplayRecord();

        output.WriteLine(evidence.ToString());
        Assert.Contains(replay.AcceptedCommands, command => command.ContentId.Equals(SampleVerticalSliceContent.SiegeCreepId));
        Assert.Contains(replay.AcceptedCommands, command => command.ContentId.Equals(SampleVerticalSliceContent.SwarmCreepId));
        // Lowered from 2. Player two is the GREEDY profile, which is defined as prioritising sends over
        // towers — one tower is correct behaviour for it. The old bar of 2 was only ever met because a
        // bot-pressure bug (a filter missing !HasLeaked) froze bots out of sending, so even a Greedy bot had
        // nothing to spend gold on but towers. The scenario's actual claim — distinct send roles plus a
        // defensive response — is carried by the Siege and Swarm sends below and player three's tower count.
        Assert.True(evidence.PlayerTwoTowers >= 1);
        // Lowered from 3: with the bot-pressure fix (a filter missing !HasLeaked previously froze bots out
        // of sending) bots now split gold between sends and towers instead of pouring it all into towers, so
        // tower counts in a fixed window are lower. The scenario's point is that a defensive bot builds
        // WHILE under pressure, which 2 still demonstrates.
        Assert.True(evidence.PlayerThreeTowers >= 2);
        Assert.True(evidence.DamageEvents >= 1);
        Assert.True(evidence.RecentBotDecisions >= 2);
    }

    private static LocalVerticalSlice CreateSlice(
        BotDecisionProfile playerTwoProfile,
        BotDecisionProfile playerThreeProfile,
        ContentId playerTwoPrimaryCreepId,
        ContentId playerThreePrimaryCreepId)
    {
        var options = new LocalMatchOptions(seed: 2_002, laneCount: 3, botLanes: new[]
        {
            new BotLaneOptions(2, profile: playerTwoProfile, primaryCreepId: playerTwoPrimaryCreepId),
            new BotLaneOptions(3, profile: playerThreeProfile, primaryCreepId: playerThreePrimaryCreepId)
        });

        return new LocalVerticalSlice(SampleVerticalSliceContent.Create(), options);
    }

    private static LocalMatchOptions ThreeLaneOptions() => new(laneCount: 3);

    private static void RunTicks(LocalVerticalSlice slice, int ticks)
    {
        for (var tick = 0; tick < ticks && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
        }
    }

    private static ScenarioEvidence CaptureEvidence(LocalVerticalSlice slice)
    {
        var snapshot = slice.GetSnapshot();
        var replay = slice.GetReplayRecord();
        var diagnostics = slice.GetBotDiagnostics();
        var events = slice.DrainEvents();

        return new ScenarioEvidence(
            snapshot.Tick.Value,
            replay.AcceptedCommands.Count,
            replay.AcceptedCommands.Count(command => command.Quantity > 1),
            diagnostics.RecentDecisions.Count,
            snapshot.Towers.Count(tower => tower.OwnerId.Equals(new PlayerId(2))),
            snapshot.Towers.Count(tower => tower.OwnerId.Equals(new PlayerId(3))),
            snapshot.Creeps.Count,
            snapshot.Creeps.Count(creep => creep.Health < MaxHealthFor(creep.CreepId)),
            snapshot.Players.Players.Sum(player => player.Income.Amount),
            snapshot.Players.Players.Sum(player => player.Lives.Amount),
            snapshot.Players.Get(new PlayerId(1)).Lives.Amount,
            events.Count(simulationEvent => simulationEvent is CreepDamagedEvent),
            events.Count(simulationEvent => simulationEvent is LeakEvent));
    }

    private static int MaxHealthFor(ContentId creepId)
    {
        if (creepId.Equals(SampleVerticalSliceContent.BruteCreepId))
        {
            return 24;
        }

        if (creepId.Equals(SampleVerticalSliceContent.SwarmCreepId))
        {
            return 5;
        }

        if (creepId.Equals(SampleVerticalSliceContent.ShadeCreepId))
        {
            return 14;
        }

        if (creepId.Equals(SampleVerticalSliceContent.SiegeCreepId))
        {
            return 48;
        }

        return 10;
    }

    private readonly record struct ScenarioEvidence(
        long Tick,
        int AcceptedCommands,
        int MultiQuantityCommands,
        int RecentBotDecisions,
        int PlayerTwoTowers,
        int PlayerThreeTowers,
        int ActiveCreeps,
        int DamagedCreeps,
        int TotalIncome,
        int TotalLives,
        int PlayerOneLives,
        int DamageEvents,
        int LeakEvents);
}
