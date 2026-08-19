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
        // Rescaled with StartingLives 220 -> 100 (2026-08-09), then 100 -> 40 (2026-08-19). These
        // were never thresholds about lives in the abstract; they encode "this seat loses at most 2
        // lives in 80 ticks" and "these 3 seats lose at most 4 between them", which is what the
        // scenario is actually about. Held at the same LOSS both times, restated against the new
        // opening balance.
        Assert.True(evidence.PlayerOneLives >= 38);
        Assert.True(evidence.DamageEvents >= 1);
        // 3 seats x 40 opening lives, less the same 4-life allowance as before.
        Assert.True(evidence.TotalLives >= 116);
        Assert.Null(slice.MatchSummary);
    }

    [Fact]
    public void Normal_pressure_scenario_records_income_and_active_combat()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
        // The scenario is about a defence being overwhelmed, not about P3's budget. Runner, Brute and
        // Siege total 136 gold on the 2x creep roster against a 100-gold opening, so the Siege send
        // below started failing on price. Granted rather than dropped, because the Siege is what makes
        // this "overwhelmed" rather than "held" — see the note on that send.
        slice.GrantLocalPlaytestGold(new PlayerId(3), new Gold(200));
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.TowerId, new GridPosition(2, 8)).Accepted);
        Assert.True(slice.PlaceTower(new PlayerId(1), new LaneId(1), SampleVerticalSliceContent.ControlTowerId, new GridPosition(4, 8)).Accepted);
        Assert.True(slice.QueueSend(new PlayerId(3), SampleVerticalSliceContent.CreepId).Accepted);
        // Spaced past the 30-tick send cooldown (enforced as of the multiplayer authority pass).
        // This scenario is about income accruing alongside active combat, not send cadence; the
        // wait is folded into the run below so total elapsed ticks stay in the same ballpark.
        RunTicks(slice, 30);
        Assert.True(slice.QueueSend(new PlayerId(3), SampleVerticalSliceContent.BruteCreepId).Accepted);

        // Pressure raised from a single Brute to a Brute plus a Siege after the 2026-07-31 stat
        // pass. Two towers used to be unable to stop one Runner and one Brute, and the leak this
        // scenario asserts came for free; now that a cheap tower can actually threaten a mid-weight
        // creep, the same pressure is simply held. Adding a heavy keeps the scenario about a defence
        // that is overwhelmed rather than about towers that could not fight — which is the thing the
        // leak assertion below is here to evidence.
        RunTicks(slice, 30);
        Assert.True(slice.QueueSend(new PlayerId(3), SampleVerticalSliceContent.SiegeCreepId).Accepted);

        RunTicks(slice, 120);

        var evidence = CaptureEvidence(slice);

        output.WriteLine(evidence.ToString());
        Assert.True(evidence.AcceptedCommands >= 2);
        Assert.True(evidence.TotalIncome > 30);
        Assert.True(evidence.DamageEvents >= 1);
        Assert.True(evidence.LeakEvents >= 1);
        Assert.Null(slice.MatchSummary);
    }

    /// <summary>
    /// One creep can leak many times, and every leak is a separate steal.
    /// </summary>
    /// <remarks>
    /// This is the interaction that makes the life-steal mechanic feel wrong in play, and it is
    /// worth pinning because neither half is obviously at fault on its own.
    ///
    /// A creep that reaches a lane end does NOT die. It costs the defender a life and then transfers
    /// to the next active opponent's lane carrying its health, by design — that is what makes this a
    /// rotating lane war. Stealing then applies per leak, so a single creep that survives its way
    /// around the carousel moves one life to the sender on every hop.
    ///
    /// Measured here with no towers on the board, so nothing kills the creep: one 18-gold Brute
    /// produces 13 leaks and moves 13 lives to the sender. That number is a worst case rather than a
    /// typical one — real lanes have towers — but the mechanism is what matters, and the same
    /// compounding is visible in batch playtests, where the winner finishes holding 1,606 of the
    /// 1,760 lives in play.
    ///
    /// The multiplier itself is NOT new and is not caused by stealing: the same creep took the same
    /// 13 lives before, it simply destroyed them instead of transferring them. Stealing made a
    /// pre-existing property of the carousel visible by concentrating its output in one player.
    /// </remarks>
    [Fact]
    public void One_surviving_creep_leaks_repeatedly_and_steals_on_every_hop()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), ThreeLaneOptions(), enableBots: false);
        var before = slice.GetSnapshot().Players.Players.ToDictionary(player => player.PlayerId.Value, player => player.Lives.Amount);
        Assert.True(slice.QueueSend(new PlayerId(3), SampleVerticalSliceContent.BruteCreepId).Accepted);

        var leaks = 0;
        for (var tick = 0; tick < 600 && slice.MatchSummary is null; tick++)
        {
            slice.AdvanceOneTick();
            leaks += slice.DrainEvents().OfType<LeakEvent>().Count();
        }

        var after = slice.GetSnapshot().Players.Players.ToDictionary(player => player.PlayerId.Value, player => player.Lives.Amount);
        output.WriteLine($"one brute, no towers -> {leaks} leaks; " + string.Join(", ", after.OrderBy(entry => entry.Key).Select(entry => $"P{entry.Key} {before[entry.Key]}->{entry.Value}")));

        // Undefended, one creep leaks many times rather than once.
        Assert.True(leaks > 1, $"expected a surviving creep to leak repeatedly, got {leaks}");

        // Every one of those leaks moved a life to the sender, and none was created or destroyed.
        Assert.Equal(before[3] + leaks, after[3]);
        Assert.Equal(before.Values.Sum(), after.Values.Sum());
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
        // 10 to 5 with the 2x creep roster (2026-08-19). A command is a purchase, and every purchase
        // in this scenario costs twice what it did, so the same gold committed over the same 360 ticks
        // buys half as many of them — the bar is restated at the same SPEND, the way the lives bounds
        // above are restated at the same loss. Measured at 7 here, and the exchange is emphatically
        // real: 69 damage events and 25 leaks.
        Assert.True(evidence.AcceptedCommands >= 5);
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
