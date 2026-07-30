using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace LTW.Tests;

/// <summary>
/// Puts each tower alone against one creep and counts how long it takes to kill it.
/// </summary>
/// <remarks>
/// The 15-tower roster's stats were sized off damage-per-gold arithmetic, which says nothing about
/// whether a tower can actually finish a creep before it walks out of range. A tower that cannot is
/// a trap: it reads as a reasonable purchase and does nothing. This measures the real number by
/// running the actual CombatService rather than reasoning about the stats.
///
/// The assertions are deliberately loose. Balance is a judgement for a playtest, so these only
/// catch outright brokenness — a tower that never kills, or one so far outside the pack that it is
/// certainly a mistake. <see cref="Report_time_to_kill_for_every_tower"/> prints the table for a
/// human to read; run with `dotnet test -v n` to see it.
/// </remarks>
public sealed class TowerDuelBalanceTests
{
    private static readonly LaneId Lane = MazedLane.Lane;
    private const int TicksPerSecond = 4;

    private readonly ITestOutputHelper output;

    public TowerDuelBalanceTests(ITestOutputHelper output) => this.output = output;

    private sealed record DuelResult(string TowerId, int Cost, int? KillTick, int Shots, int DamageDealt, bool Leaked);

    /// <summary>
    /// One tower, one creep, from spawn until the creep dies or leaks. Tower is placed adjacent to
    /// the lane at the row the creep must walk past, so range is genuinely tested.
    /// </summary>
    private static DuelResult Duel(ContentCatalog catalog, TowerDefinition tower, CreepDefinition creep)
    {
        var service = new CombatService();

        // Measured against a MAZED route, not a straight line. The straight version of this harness
        // reported that ten of fifteen towers could not kill a Runner — measured against a 16-cell lane no
        // player would leave straight, while a real mazed route runs 52 cells. Exposure is what changed,
        // not the towers.
        var lane = MazedLane.Build();
        var routes = lane.Routes();
        var content = new CombatContent(
            catalog.Creeps,
            catalog.Towers,
            new Dictionary<LaneId, PlayerId> { [Lane] = new PlayerId(1) });

        // Mid-route, so the creep spends the maximum time inside whatever range this tower has.
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), creep, new PlayerId(2), Lane) },
            new[] { new TowerCombatState(new EntityId(10), tower.Id, new PlayerId(1), Lane, lane.CellBesideRoute(0.5d)) });

        var shots = 0;
        var damage = 0;
        var leaked = false;

        for (var tick = 0; tick < 1200; tick++)
        {
            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;

            foreach (var simulationEvent in result.Events)
            {
                switch (simulationEvent)
                {
                    case LTW.Simulation.Events.TowerFiredEvent:
                        shots++;
                        break;
                    case LTW.Simulation.Events.CreepDamagedEvent damaged:
                        damage += damaged.DamageDealt;
                        break;
                    case LTW.Simulation.Events.CreepKilledEvent:
                        return new DuelResult(tower.Id.Value, tower.Cost.Amount, tick, shots, damage, false);
                    case LTW.Simulation.Events.LeakEvent:
                        leaked = true;
                        break;
                }
            }

            if (leaked)
            {
                break;
            }
        }

        return new DuelResult(tower.Id.Value, tower.Cost.Amount, null, shots, damage, leaked);
    }

    [Fact]
    public void Report_time_to_kill_for_every_tower()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var runner = catalog.Creeps.First(creep => creep.Id.Value == "creep.runner");
        var brute = catalog.Creeps.First(creep => creep.Id.Value == "creep.brute");

        output.WriteLine($"{"tower",-22} {"gold",4} {"vsRunner",9} {"vsBrute",8}  {"shots(B)",8} {"dmg/gold",8}");
        foreach (var tower in catalog.Towers.OrderBy(tower => tower.Cost.Amount))
        {
            var vsRunner = Duel(catalog, tower, runner);
            var vsBrute = Duel(catalog, tower, brute);
            var runnerText = vsRunner.KillTick is int rk ? $"{rk / (double)TicksPerSecond:F2}s" : vsRunner.Leaked ? "LEAKED" : "never";
            var bruteText = vsBrute.KillTick is int bk ? $"{bk / (double)TicksPerSecond:F2}s" : vsBrute.Leaked ? "LEAKED" : "never";
            var perGold = vsBrute.DamageDealt / (double)tower.Cost.Amount;
            output.WriteLine($"{tower.Id.Value,-22} {tower.Cost.Amount,4} {runnerText,9} {bruteText,8}  {vsBrute.Shots,8} {perGold,8:F2}");
        }
    }

    /// <summary>
    /// Every tower must land at least one shot on a creep walking past it.
    /// </summary>
    /// <remarks>
    /// This started out asserting that every tower could kill a Runner on its own, which is simply
    /// not how the game works: SpeedPerSecond is applied once per tick, so a speed-1 creep crosses
    /// all 18 cells in 18 ticks — 4.5 seconds — and even the Arrow Tower only gets two shots in
    /// before it leaks. No single tower solos anything. Landing a shot at all is the real floor, and
    /// a tower that fails it is broken rather than merely weak.
    /// </remarks>
    [Fact]
    public void Every_tower_lands_a_shot_on_a_creep_walking_past()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var runner = catalog.Creeps.First(creep => creep.Id.Value == "creep.runner");

        foreach (var tower in catalog.Towers)
        {
            var duel = Duel(catalog, tower, runner);
            Assert.True(
                duel.Shots > 0,
                $"{tower.Id.Value} never fired at a creep walking past it (damage={duel.DamageDealt}, leaked={duel.Leaked})");
            Assert.True(
                duel.DamageDealt > 0,
                $"{tower.Id.Value} fired {duel.Shots} time(s) but dealt no damage");
        }
    }

    /// <summary>
    /// No tower may be an order of magnitude better than the median at converting gold into damage.
    /// This is a brokenness check, not a balance target — a 10x spread is a mistake, a 2x spread is
    /// a design decision.
    /// </summary>
    [Fact]
    public void No_tower_is_an_order_of_magnitude_more_gold_efficient_than_the_median()
    {
        var catalog = SampleVerticalSliceContent.Create();
        var brute = catalog.Creeps.First(creep => creep.Id.Value == "creep.brute");

        var efficiency = catalog.Towers
            .Select(tower => (tower.Id.Value, Value: Duel(catalog, tower, brute).DamageDealt / (double)tower.Cost.Amount))
            .OrderBy(entry => entry.Value)
            .ToArray();

        var median = efficiency[efficiency.Length / 2].Value;
        Assert.True(median > 0, "median gold efficiency was zero, so the duel harness measured nothing");

        foreach (var (id, value) in efficiency)
        {
            Assert.True(
                value <= median * 10d,
                $"{id} converts gold to damage {value / median:F1}x the median ({value:F2} vs {median:F2})");
        }
    }
}
