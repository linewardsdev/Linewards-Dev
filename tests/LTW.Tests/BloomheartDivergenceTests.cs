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
/// Measures how often Reaping Bloom actually changes which creep the Bloomheart Totem shoots.
/// </summary>
/// <remarks>
/// The concern this answers: a quantity-N send spawns all N creeps on the same cell, in the same tick,
/// at full health, and they then move as a pure function of position and speed — so they stay stacked
/// and identical. Fed to Reaping Bloom's four tie-breakers (is-lethal, lowest health, furthest forward,
/// entity id) every key ties and the last one decides, which picks the same creep the default
/// front-most rule would. In that case the mechanic is not broken, it simply never engages, and the
/// tower is the Arrow Tower with different numbers.
///
/// Method: run each scenario twice against identical state, once with the real Bloomheart and once with
/// a BASELINE tower carrying Bloomheart's exact stats under an id the mechanic does not match. The
/// difference in what they shoot is the mechanic's entire contribution. Nothing is inferred — both runs
/// are the real CombatService, and the comparison is over TowerFiredEvent target ids tick by tick.
/// </remarks>
public sealed class BloomheartDivergenceTests
{
    private static readonly LaneId Lane = new(1);
    private static readonly PlayerId Defender = new(1);
    private static readonly PlayerId Attacker = new(2);
    private static readonly ContentId BloomheartId = new("tower.bloomheart");

    /// <summary>Bloomheart's stats under an id Reaping Bloom does not match, so it uses the default rule.</summary>
    private static readonly ContentId BaselineId = new("tower.baseline_totem");

    private readonly ITestOutputHelper output;

    public BloomheartDivergenceTests(ITestOutputHelper output) => this.output = output;

    private static ContentCatalog Catalog()
    {
        var real = SampleVerticalSliceContent.Create();
        var bloomheart = real.Towers.Single(tower => tower.Id.Equals(BloomheartId));
        var baseline = new TowerDefinition(
            BaselineId,
            "Baseline Totem",
            bloomheart.Cost,
            bloomheart.RangeCells,
            bloomheart.Damage,
            bloomheart.AttackCooldownTicks);

        return new ContentCatalog(
            real.Version,
            real.Towers.Concat(new[] { baseline }).ToArray(),
            real.Creeps,
            real.Techs,
            real.Maps,
            real.BotProfiles);
    }

    private static IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> Routes() =>
        new Dictionary<LaneId, IReadOnlyList<GridPosition>>
        {
            [Lane] = Enumerable.Range(0, 18).Select(y => new GridPosition(3, y)).ToArray()
        };

    /// <summary>
    /// A scenario is a recipe for seeding creeps, so both runs get bit-identical starting state.
    /// </summary>
    private sealed record Scenario(string Name, System.Func<CombatService, CreepDefinition, int, IEnumerable<CreepCombatState>> Seed, string CreepId, bool WithSupport);

    /// <summary>
    /// Runs both towers over the same scenario and compares their shot-by-shot target choice.
    /// </summary>
    private static (int Shots, int Diverged, int BloomKills, int BaselineKills) Compare(Scenario scenario)
    {
        var bloom = RunTrace(scenario, BloomheartId);
        var baseline = RunTrace(scenario, BaselineId);

        var shots = System.Math.Min(bloom.Targets.Count, baseline.Targets.Count);
        var diverged = 0;
        for (var index = 0; index < shots; index++)
        {
            if (bloom.Targets[index] != baseline.Targets[index])
            {
                diverged++;
            }
        }

        // A different shot count is itself divergence: the rule changed the sequence enough that one
        // run fired more often than the other.
        diverged += System.Math.Abs(bloom.Targets.Count - baseline.Targets.Count);

        return (System.Math.Max(bloom.Targets.Count, baseline.Targets.Count), diverged, bloom.Kills, baseline.Kills);
    }

    private sealed record Trace(List<long> Targets, int Kills, int Damage);

    private static Trace RunTrace(Scenario scenario, ContentId towerId)
    {
        var catalog = Catalog();
        var service = new CombatService();
        var content = new CombatContent(
            catalog.Creeps,
            catalog.Towers,
            new Dictionary<LaneId, PlayerId> { [Lane] = Defender });
        var routes = Routes();
        var definition = catalog.Creeps.Single(creep => creep.Id.Value == scenario.CreepId);

        var towers = new List<TowerCombatState>
        {
            new(new EntityId(100), towerId, Defender, Lane, new GridPosition(2, 9))
        };
        if (scenario.WithSupport)
        {
            towers.Add(new TowerCombatState(new EntityId(101), new ContentId("tower.gatling"), Defender, Lane, new GridPosition(4, 8)));
        }

        var state = new CombatState(System.Array.Empty<CreepCombatState>(), towers);
        var targets = new List<long>();
        var kills = 0;
        var damage = 0;

        for (var tick = 0; tick < 120; tick++)
        {
            var seeded = scenario.Seed(service, definition, tick).ToArray();
            if (seeded.Length > 0)
            {
                state = new CombatState(state.Creeps.Concat(seeded), state.Towers);
            }

            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;

            var fired = result.Events.OfType<TowerFiredEvent>()
                .FirstOrDefault(f => f.TowerEntityId.Equals(new EntityId(100)));
            if (fired is not null)
            {
                targets.Add(fired.TargetCreepEntityId.Value);
            }

            kills += result.Events.OfType<CreepKilledEvent>().Count();
            damage += result.Events.OfType<CreepDamagedEvent>()
                .Where(d => d.TowerEntityId.Equals(new EntityId(100)))
                .Sum(d => d.DamageDealt);
        }

        return new Trace(targets, kills, damage);
    }

    private static IEnumerable<CreepCombatState> None() => System.Array.Empty<CreepCombatState>();

    private static Scenario[] Scenarios() => new[]
    {
        // THE MODAL CASE. A quantity-6 send: all six spawn on one tick, one cell, full health.
        new Scenario("same-type send x6 (modal)", (service, definition, tick) =>
            tick == 0
                ? Enumerable.Range(1, 6).Select(index => service.SpawnCreep(new EntityId(index), definition, Attacker, Lane))
                : None(),
            "creep.brute",
            WithSupport: false),

        // Same, but with another tower chipping them, so health starts to differ organically.
        new Scenario("same-type send x6 + support", (service, definition, tick) =>
            tick == 0
                ? Enumerable.Range(1, 6).Select(index => service.SpawnCreep(new EntityId(index), definition, Attacker, Lane))
                : None(),
            "creep.brute",
            WithSupport: true),

        // Trickle: one creep every 3 ticks, so path indices differ but health does not.
        new Scenario("trickle x8", (service, definition, tick) =>
            tick % 3 == 0 && tick < 24
                ? new[] { service.SpawnCreep(new EntityId(tick / 3 + 1), definition, Attacker, Lane) }
                : None(),
            "creep.brute",
            WithSupport: false),

        new Scenario("trickle x8 + support", (service, definition, tick) =>
            tick % 3 == 0 && tick < 24
                ? new[] { service.SpawnCreep(new EntityId(tick / 3 + 1), definition, Attacker, Lane) }
                : None(),
            "creep.brute",
            WithSupport: true),

        // A wounded trailer behind healthy leaders — the case the mechanic was designed for.
        new Scenario("wounded trailer", (service, definition, tick) =>
            tick == 0
                ? new[]
                {
                    service.SpawnCreep(new EntityId(1), definition, Attacker, Lane).WithMovement(6, 0),
                    service.SpawnCreep(new EntityId(2), definition, Attacker, Lane).WithMovement(5, 0),
                    service.SpawnCreep(new EntityId(3), definition, Attacker, Lane).WithMovement(4, 0).WithHealth(3)
                }
                : None(),
            "creep.brute",
            WithSupport: false),
    };

    [Fact]
    public void Report_how_often_reaping_bloom_changes_the_shot()
    {
        output.WriteLine($"{"scenario",-30} {"shots",6} {"diverged",9} {"rate",6}  {"damage bloom/base",18}");
        var totalShots = 0;
        var totalDiverged = 0;

        foreach (var scenario in Scenarios())
        {
            var (shots, diverged, bloomKills, baselineKills) = Compare(scenario);
            var bloomDamage = RunTrace(scenario, BloomheartId).Damage;
            var baselineDamage = RunTrace(scenario, BaselineId).Damage;
            totalShots += shots;
            totalDiverged += diverged;
            var rate = shots == 0 ? 0d : diverged / (double)shots;
            output.WriteLine($"{scenario.Name,-30} {shots,6} {diverged,9} {rate,6:P0}  {bloomDamage,8}/{baselineDamage,-9}");
        }

        var overall = totalShots == 0 ? 0d : totalDiverged / (double)totalShots;
        output.WriteLine($"{"OVERALL",-30} {totalShots,6} {totalDiverged,9} {overall,6:P0}");
    }

    /// <summary>
    /// The modal case is the whole question, and it is now where the mechanic WORKS.
    /// </summary>
    /// <remarks>
    /// Crowd Bloom is a damage rule, not a selection rule, so divergence is measured as damage rather
    /// than as which creep gets shot. Against a stacked same-type send the totem must out-damage a
    /// stat-identical tower without the mechanic — this is the exact scenario in which its predecessor
    /// measured 0%.
    /// </remarks>
    [Fact]
    public void Crowd_bloom_out_damages_a_stat_identical_tower_against_a_stacked_send()
    {
        var bloom = RunTrace(Scenarios()[0], BloomheartId);
        var baseline = RunTrace(Scenarios()[0], BaselineId);

        Assert.True(bloom.Damage > 0, "the totem never fired, so nothing was measured");
        Assert.True(
            bloom.Damage > baseline.Damage,
            $"Crowd Bloom dealt {bloom.Damage} against the baseline's {baseline.Damage} — the mechanic is inert in the modal case");
    }

    /// <summary>
    /// The flip side: spread out in single file there is no crowd, so the tower must be exactly its
    /// baseline. A mechanic that always applies is just a bigger damage number.
    /// </summary>
    [Fact]
    public void Crowd_bloom_matches_the_baseline_when_creeps_arrive_in_single_file()
    {
        var bloom = RunTrace(Scenarios()[2], BloomheartId);
        var baseline = RunTrace(Scenarios()[2], BaselineId);

        Assert.True(bloom.Damage > 0, "the totem never fired, so nothing was measured");
        Assert.Equal(baseline.Damage, bloom.Damage);
    }
}
