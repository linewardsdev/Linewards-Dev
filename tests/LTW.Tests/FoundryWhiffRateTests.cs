using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Measures how often a Foundry Core's shell lands on nothing.
/// </summary>
/// <remarks>
/// This is the mortar's ship/no-ship gate. A 52-gold tower that visibly does nothing some fraction of
/// the time is a trap purchase, and the whiff sources compound in exactly the situation where the
/// player has built WELL: an over-defended lane kills the led target before the shell arrives.
///
/// A whiff is a shell that resolves with zero <see cref="CreepDamagedEvent"/>s. There are three
/// distinct causes and they need separating, because they have different fixes:
///   1. the led target died to another tower during the flight  — fix: shorter flight
///   2. the led cell was past the end of the route (the creep leaked) — fix: don't fire that late
///   3. the creep changed speed mid-flight (bramble) — already handled, since the lead uses StepCreep
/// </remarks>
public sealed class FoundryWhiffRateTests
{
    private static readonly LaneId Lane = MazedLane.Lane;
    private static readonly PlayerId Defender = new(1);
    private static readonly PlayerId Attacker = new(2);
    private static readonly ContentCatalog Catalog = SampleVerticalSliceContent.Create();

    private readonly ITestOutputHelper output;

    public FoundryWhiffRateTests(ITestOutputHelper output) => this.output = output;

    /// <summary>
    /// A real mazed lane. The straight-lane version of this harness produced the 100%-whiff finding that
    /// led to the lead filter, and that finding still stands — but a mortar's lead depends on where the
    /// route goes, so the rate has to be confirmed against the route players actually build.
    /// </summary>
    private static readonly MazedLane Lane0 = MazedLane.Build();

    private static IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> Routes() => Lane0.Routes();

    private static CombatContent Content() => new(
        Catalog.Creeps,
        Catalog.Towers,
        new Dictionary<LaneId, PlayerId> { [Lane] = Defender });

    private static CreepDefinition Creep(string id) => Catalog.Creeps.First(creep => creep.Id.Value == id);

    private sealed record Outcome(int Shells, int Whiffs)
    {
        public double WhiffRate => Shells == 0 ? 0d : Whiffs / (double)Shells;
    }

    /// <summary>
    /// Runs a Foundry at <paramref name="foundryRow"/> against a stream of creeps, optionally with a
    /// supporting Gatling to create the "well defended lane" case that causes the worst whiffs.
    /// </summary>
    private static Outcome Run(string creepId, double foundryFraction, bool withSupport, int creepCount)
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();
        var definition = Creep(creepId);

        var towers = new List<TowerCombatState>
        {
            new(new EntityId(100), new ContentId("tower.foundry"), Defender, Lane, Lane0.CellBesideRoute(foundryFraction))
        };
        if (withSupport)
        {
            // Deliberately upstream of the Foundry, so it finishes creeps the mortar has already
            // committed a shell to.
            var support = Lane0.FreeNeighboursOf(Lane0.CellBesideRoute(foundryFraction));
            towers.Add(new TowerCombatState(new EntityId(101), new ContentId("tower.gatling"), Defender, Lane, support[0]));
        }

        var state = new CombatState(System.Array.Empty<CreepCombatState>(), towers);
        var nextCreep = 1;
        var shells = 0;
        var whiffs = 0;
        var pendingImpactTick = -1;

        for (var tick = 0; tick < 400 && nextCreep <= creepCount; tick++)
        {
            // Trickle creeps in so the lane always has something to shoot at.
            if (tick % 3 == 0)
            {
                state = new CombatState(
                    state.Creeps.Concat(new[] { service.SpawnCreep(new EntityId(nextCreep++), definition, Attacker, Lane) }),
                    state.Towers);
            }

            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;

            var launched = result.Events.OfType<TowerFiredEvent>()
                .FirstOrDefault(fired => fired.TowerEntityId.Equals(new EntityId(100)));
            if (launched is not null)
            {
                shells++;
                pendingImpactTick = (int)launched.ImpactTick.Value;
            }

            if (pendingImpactTick == tick)
            {
                var landed = result.Events.OfType<CreepDamagedEvent>()
                    .Any(damaged => damaged.TowerEntityId.Equals(new EntityId(100)));
                if (!landed)
                {
                    whiffs++;
                }

                pendingImpactTick = -1;
            }
        }

        return new Outcome(shells, whiffs);
    }

    [Fact]
    public void Report_whiff_rate_by_row_and_creep_speed()
    {
        output.WriteLine("creep            speed  row  support  shells  whiffs  rate");
        foreach (var creepId in new[] { "creep.runner", "creep.swarm", "creep.wisp" })
        {
            var speed = Creep(creepId).SpeedPerSecond;
            foreach (var row in new[] { 0.2d, 0.45d, 0.7d, 0.9d })
            {
                foreach (var support in new[] { false, true })
                {
                    var outcome = Run(creepId, row, support, creepCount: 40);
                    output.WriteLine(
                        $"{creepId,-16} {speed,5}  {row,5:F2}  {support,7}  {outcome.Shells,6}  {outcome.Whiffs,6}  {outcome.WhiffRate,5:P0}");
                }
            }
        }
    }

    /// <summary>
    /// A Foundry placed where the lane is long enough ahead of it must not whiff at all against a
    /// slow creep with nothing else shooting. Any whiff there is a lead-arithmetic bug, not a
    /// gameplay outcome.
    /// </summary>
    [Fact]
    public void Foundry_never_whiffs_on_a_slow_creep_with_room_ahead_and_no_support()
    {
        var outcome = Run("creep.brute", foundryFraction: 0.4d, withSupport: false, creepCount: 30);

        Assert.True(outcome.Shells > 0, "the Foundry never fired, so nothing was measured");
        Assert.Equal(0, outcome.Whiffs);
    }

    /// <summary>
    /// The gate: a shell that leaves the stacks must land on something.
    /// </summary>
    /// <remarks>
    /// Originally written to allow up to a third, on the assumption whiffs would be occasional and
    /// spread out. Measurement showed the opposite shape — 0% almost everywhere and 100% in a few
    /// specific configurations (a Foundry in the last rows against a speed-2 or speed-3 creep, where
    /// the lead lands on the leak index and can never be hit). That is a systematic dead zone, not
    /// dice, so the tower now declines to fire rather than committing a shell it cannot land, and the
    /// bar is zero.
    /// </remarks>
    [Fact]
    public void A_launched_shell_always_lands_on_something()
    {
        var shells = 0;
        var whiffs = 0;
        foreach (var creepId in new[] { "creep.runner", "creep.swarm", "creep.wisp" })
        {
            foreach (var row in new[] { 0.2d, 0.45d, 0.7d, 0.9d })
            {
                foreach (var support in new[] { false, true })
                {
                    var outcome = Run(creepId, row, support, creepCount: 40);
                    shells += outcome.Shells;
                    whiffs += outcome.Whiffs;
                }
            }
        }

        Assert.True(shells > 50, $"only {shells} shells measured, too few to conclude anything");
        Assert.Equal(0, whiffs);
    }

    /// <summary>
    /// The flip side of the gate: declining to fire must be RARE, not the normal case. If the lead
    /// filter silenced the tower broadly it would have traded a visible failure for an invisible one.
    /// </summary>
    [Fact]
    public void Foundry_still_fires_in_the_large_majority_of_placements()
    {
        var firing = 0;
        var total = 0;
        foreach (var creepId in new[] { "creep.runner", "creep.swarm", "creep.wisp" })
        {
            foreach (var row in new[] { 0.2d, 0.45d, 0.7d, 0.9d })
            {
                total++;
                if (Run(creepId, row, withSupport: false, creepCount: 40).Shells > 0)
                {
                    firing++;
                }
            }
        }

        Assert.True(firing * 4 >= total * 3, $"Foundry only fired in {firing} of {total} placements");
    }
}
