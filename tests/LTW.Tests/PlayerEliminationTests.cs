using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// A defeated seat's lane is cleared and the match carries on around it.
/// </summary>
/// <remarks>
/// Elimination used to be an economy-only fact — no income, no sends, skipped when routing — while
/// CombatService knew nothing about it. A defeated player's towers kept firing and creeps kept
/// walking their lane, leaking against somebody already on zero lives.
/// </remarks>
public sealed class PlayerEliminationTests
{
    private readonly ITestOutputHelper output;
    public PlayerEliminationTests(ITestOutputHelper output) => this.output = output;

    /// <summary>Drains one seat to zero lives by repeatedly sending at it, with no defence built.</summary>
    private static LocalVerticalSlice RunUntilEliminated(out PlayerId victim, out int atTick)
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 7, laneCount: 3), enableBots: false);
        var eliminated = (PlayerId?)null;
        atTick = 0;

        for (var tick = 0; tick < 40_000 && eliminated is null; tick++)
        {
            slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.SiegeCreepId);
            slice.AdvanceOneTick();
            foreach (var e in slice.DrainEvents())
            {
                if (e is PlayerEliminatedEvent pe && eliminated is null)
                {
                    eliminated = pe.PlayerId;
                    atTick = tick;
                }
            }
        }

        Assert.NotNull(eliminated);
        victim = eliminated!.Value;
        return slice;
    }

    [Fact]
    public void An_eliminated_players_lane_is_cleared_and_the_match_continues()
    {
        var slice = RunUntilEliminated(out var victim, out var atTick);
        var lane = new LaneId(victim.Value);
        var snapshot = slice.GetSnapshot();

        output.WriteLine($"eliminated P{victim.Value} at tick {atTick}");

        Assert.DoesNotContain(snapshot.Towers, t => t.OwnerId.Equals(victim));
        Assert.DoesNotContain(snapshot.Creeps, c => c.LaneId.Equals(lane));
        Assert.Null(slice.MatchSummary);

        // The route must be rebuilt, not merely emptied of towers: towers are what lengthen it, so
        // a stale route would leave creeps walking around a maze that no longer exists.
        Assert.Equal(MazedLane.StraightRoute().Count, slice.RouteLength(lane));
    }

    [Fact]
    public void Wiping_a_lane_leaves_other_lanes_playing_but_removes_none_of_the_eliminated_players_own_creeps_elsewhere()
    {
        var slice = RunUntilEliminated(out var victim, out _);
        var lane = new LaneId(victim.Value);
        var snapshot = slice.GetSnapshot();

        // Only the defeated seat's lane is cleared as a LANE; every other lane keeps playing.
        Assert.All(snapshot.Creeps, creep => Assert.NotEqual(lane, creep.LaneId));
        Assert.Contains(snapshot.Players.Players, p => !p.IsEliminated);

        // Creeps the eliminated player SENT die with them too, wherever they currently are — see
        // Wiping_a_lane_also_kills_the_eliminated_players_creeps_walking_someone_elses_lane below
        // for the deterministic version of this. Renamed from "...own_creeps_alone", which asserted
        // the opposite of this on nothing but a name and an unused count: those creeps used to keep
        // marching and leaking against a life that could never be credited to anyone (reported from
        // play 2026-08-29), so this test's own name was the last place that claim survived.
        Assert.DoesNotContain(snapshot.Creeps, creep => creep.SenderId.Equals(victim));
    }

    /// <summary>
    /// A creep an eliminated seat SENT dies immediately too, even though it is walking a lane that
    /// belongs to somebody else entirely.
    /// </summary>
    /// <remarks>
    /// Deterministic companion to the test above, which only observes whatever an organic 40,000-tick
    /// elimination happened to leave mid-flight. Built by hand instead: P1 sends into P2's lane, P1
    /// is eliminated directly (not P2), and the creep P1 sent is still sitting in P2's lane at that
    /// moment — the exact shape of the reported bug, with nothing left to chance.
    ///
    /// The reported defect was two-sided and this pins both: the creep must actually be gone (not
    /// merely orphaned and still marching), and the CreepKilledEvent reporting it must name P2 — the
    /// lane's real, living defender — as DefenderId, not P1, who has nothing left to be defended.
    /// </remarks>
    [Fact]
    public void Wiping_a_lane_also_kills_the_eliminated_players_creeps_walking_someone_elses_lane()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 1, laneCount: 3), enableBots: false);
        var sender = new PlayerId(1);
        var defenderLane = new LaneId(2);

        Assert.True(slice.QueueSend(sender, SampleVerticalSliceContent.CreepId).Accepted);
        slice.AdvanceOneTick();
        slice.DrainEvents();

        var inFlight = Assert.Single(slice.GetSnapshot().Creeps);
        Assert.Equal(sender, inFlight.SenderId);
        Assert.Equal(defenderLane, inFlight.LaneId);

        slice.EliminateForLocalPlaytest(sender);

        Assert.Empty(slice.GetSnapshot().Creeps);

        var killed = Assert.Single(slice.DrainEvents().OfType<CreepKilledEvent>());
        Assert.Equal(inFlight.EntityId, killed.CreepEntityId);
        Assert.Equal(new PlayerId(2), killed.DefenderId);
    }

    /// <summary>
    /// A defeated seat earns nothing, and its state says so without the reader having to know why.
    /// </summary>
    /// <remarks>
    /// The gap this closes is a reporting one, not a payment one — the payment was always correct.
    /// `Income` keeps the value the seat built up, because that is what the match summary reports
    /// and what elimination does not undo, so a presentation layer reading `Income` reads a live
    /// number that means "the economy this player had" and shows it as "what this player earns".
    /// That is how the Unity HUD kept advertising +10 for a seat being paid zero (item 31).
    ///
    /// Asserting `EffectiveIncome` alone would be a tautology over its own one-line body. What makes
    /// this a test is measuring the gold across a real income tick and requiring the two to agree,
    /// so the derived value cannot drift from the `ApplyIncomeTick` filter that does the paying.
    /// </remarks>
    [Fact]
    public void EliminatedSeatEarnsNothingAndReportsThat()
    {
        var slice = RunUntilEliminated(out var victim, out _);
        var survivor = slice.GetSnapshot().Players.Players.First(p => !p.IsEliminated).PlayerId;

        var before = slice.GetSnapshot().Players;
        Assert.True(before.Get(victim).Income.Amount > 0, "the economy the seat built is kept, not erased");
        Assert.Equal(0, before.Get(victim).EffectiveIncome.Amount);
        Assert.Equal(before.Get(survivor).Income.Amount, before.Get(survivor).EffectiveIncome.Amount);

        var victimGoldBefore = before.Get(victim).Gold.Amount;
        var survivorGoldBefore = before.Get(survivor).Gold.Amount;
        var incomeTicks = 0;
        var creditedToVictim = 0;

        // A whole income interval, with nothing sent, so the only gold movement available is income
        // itself. The eliminated seat has no towers left to earn signal gold and is credited nothing
        // for its own in-flight creeps, so its gold is frozen unless income pays it.
        for (var i = 0; i < slice.IncomeIntervalTicks + 1; i++)
        {
            slice.AdvanceOneTick();
            foreach (var e in slice.DrainEvents())
            {
                if (e is IncomeTickEvent income)
                {
                    incomeTicks++;
                    if (income.PlayerId.Equals(victim))
                    {
                        creditedToVictim += income.GoldAwarded.Amount;
                    }
                }
            }
        }

        var after = slice.GetSnapshot().Players;
        output.WriteLine($"P{victim.Value} eliminated: income {after.Get(victim).Income.Amount}, effective {after.Get(victim).EffectiveIncome.Amount}, gold {victimGoldBefore} -> {after.Get(victim).Gold.Amount}");

        Assert.True(incomeTicks > 0, "an income tick must have happened for this to be measuring anything");
        Assert.Equal(0, creditedToVictim);
        Assert.Equal(victimGoldBefore, after.Get(victim).Gold.Amount);
        Assert.True(after.Get(survivor).Gold.Amount > survivorGoldBefore, "an active seat is still paid");
    }

    [Fact]
    public void The_wipe_announces_each_redirected_or_removed_entity_so_presentation_can_react()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 7, laneCount: 3), enableBots: false);
        var sawEliminated = false;
        var soldOnWipe = 0;
        var redirectedOnWipe = 0;

        for (var tick = 0; tick < 40_000 && !sawEliminated; tick++)
        {
            slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.SiegeCreepId);
            slice.AdvanceOneTick();
            var events = slice.DrainEvents();
            if (events.Any(e => e is PlayerEliminatedEvent))
            {
                sawEliminated = true;
                soldOnWipe = events.Count(e => e is TowerSoldEvent);
                redirectedOnWipe = events.Count(e => e is CreepSpawnedEvent);
            }
        }

        Assert.True(sawEliminated);
        // No towers were ever built in this scenario, so the meaningful signal is the creeps that
        // were mid-lane when their target died. Requested live, 2026-09-05: they now redirect to
        // the next active opponent (CreepSpawnedEvent) rather than being killed outright, since
        // this 3-lane scenario always has another active player for them to attack — see
        // WipeEliminatedLane's own remarks (case 2) for why that is the deliberate design now.
        output.WriteLine($"on the wipe tick: {soldOnWipe} tower-sold, {redirectedOnWipe} creep-redirected");
        Assert.True(redirectedOnWipe >= 1, "the creeps standing in the wiped lane should redirect to the next active opponent");
    }

    /// <summary>
    /// The precise version of the test above: not just that a redirect happens, but that it goes
    /// to the RIGHT seat.
    /// </summary>
    [Fact]
    public void Wiping_a_lane_redirects_its_attacking_creeps_to_the_next_active_opponent()
    {
        var slice = new LocalVerticalSlice(SampleVerticalSliceContent.Create(), new LocalMatchOptions(seed: 7, laneCount: 3), enableBots: false);
        System.Collections.Generic.IReadOnlyList<ISimulationEvent> eventsAtElimination = System.Array.Empty<ISimulationEvent>();
        var sawEliminated = false;

        for (var tick = 0; tick < 40_000 && !sawEliminated; tick++)
        {
            slice.QueueSend(new PlayerId(1), SampleVerticalSliceContent.SiegeCreepId);
            slice.AdvanceOneTick();
            var events = slice.DrainEvents();
            if (events.Any(e => e is PlayerEliminatedEvent))
            {
                sawEliminated = true;
                eventsAtElimination = events;
            }
        }

        Assert.True(sawEliminated);

        // Checked against elimination order within the tick, not "any CreepSpawnedEvent in the
        // same tick": sending continuously at two undefended seats can eliminate both within one
        // tick (one leak's elimination check runs before the next leak in the same AdvanceOneTick
        // call is processed), and the PRE-EXISTING leak-survival transfer (unrelated to this
        // change — every leak, fatal or not, already tries to carry its creep on to the next
        // active opponent) can legitimately target a seat that is ALSO eliminated later in that
        // same tick, before this player's own elimination is even known. Found writing this test:
        // asserting against the tick's first elimination flagged exactly that as a false failure.
        // The real invariant is narrower and still meaningful: nothing ever redirects to a seat
        // that was ALREADY eliminated earlier in the same event stream.
        var eliminatedSoFar = new System.Collections.Generic.List<PlayerId>();
        var redirectCount = 0;
        foreach (var e in eventsAtElimination)
        {
            if (e is PlayerEliminatedEvent pe)
            {
                eliminatedSoFar.Add(pe.PlayerId);
            }
            else if (e is CreepSpawnedEvent spawned && spawned.SenderId.Equals(new PlayerId(1)))
            {
                redirectCount++;
                Assert.DoesNotContain(spawned.DefenderId, eliminatedSoFar);
            }
        }

        Assert.True(redirectCount >= 1, "at least one creep should redirect during the wipe");
    }
}
