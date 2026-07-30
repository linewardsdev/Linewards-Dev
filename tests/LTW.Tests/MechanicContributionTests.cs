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
/// Measures what each tower's special behaviour actually contributes, by running it against a
/// stat-identical tower that has no behaviour at all.
/// </summary>
/// <remarks>
/// Two mechanics have already failed this way for structurally similar reasons, and neither was visible
/// by eye. Reaping Bloom was a selection rule whose tie-breakers all tied in the common case, so it
/// picked the same creep the default rule would. Repair Drone's +1 range only helped a tower idling for
/// want of a target, so it paid out in the sparse case and paid nothing under pressure. Both looked
/// perfectly reasonable in code.
///
/// The method: a baseline tower id carrying identical cost, range, damage and cooldown, chosen so
/// CombatService's ContainsRole checks do not match it. Run the same scenario against both and diff the
/// output. The difference IS the mechanic, measured through the real simulation.
///
/// Which output matters differs by mechanic, so each case names its own metric rather than pretending one
/// number fits all:
///   damage mechanics (Grovebond, Rot, Crowd Bloom, Chain Arc) — the tower's own damage
///   support mechanics (Servicing)                             — the NEIGHBOUR's damage
///   utility mechanics (Bramble Hold)                          — total lane damage
///   selection mechanics (Deep Roots)                          — which creep gets shot
/// The Barricade is deliberately absent: it is a RESTRICTION whose stats already include the
/// compensation, so against a stat-identical baseline it must do less, and "less" is not a failure.
/// </remarks>
public sealed class MechanicContributionTests
{
    private static readonly LaneId Lane = new(1);
    private static readonly PlayerId Defender = new(1);
    private static readonly PlayerId Attacker = new(2);

    /// <summary>Contains no role token CombatService looks for, so no mechanic matches it.</summary>
    private const string PlainId = "tower.plain_unit";

    private readonly ITestOutputHelper output;

    public MechanicContributionTests(ITestOutputHelper output) => this.output = output;

    private static ContentCatalog CatalogMirroring(string towerId)
    {
        var real = SampleVerticalSliceContent.Create();
        var source = real.Towers.Single(tower => tower.Id.Value == towerId);
        var plain = new TowerDefinition(
            new ContentId(PlainId),
            "Plain Unit",
            source.Cost,
            source.RangeCells,
            source.Damage,
            source.AttackCooldownTicks);

        return new ContentCatalog(
            real.Version,
            real.Towers.Concat(new[] { plain }).ToArray(),
            real.Creeps,
            real.Techs,
            real.Maps,
            real.BotProfiles);
    }

    private static IReadOnlyList<GridPosition> Route() =>
        Enumerable.Range(0, 18).Select(y => new GridPosition(3, y)).ToArray();

    private sealed record Placement(string TowerId, int X, int Y);

    private sealed record Outcome(int SubjectDamage, int OtherTowerDamage, int TotalDamage, List<long> Targets);

    /// <summary>
    /// Runs one scenario. The tower under test is always entity 100, so its own contribution can be
    /// separated from everything else on the board.
    /// </summary>
    /// <param name="stackedSend">
    /// true seeds every creep on one tick and one cell, which is what a quantity-N send actually looks
    /// like; false trickles them so they arrive in single file. Mechanics tend to be sensitive to which,
    /// and the modal case is the stacked one.
    /// </param>
    private static Outcome Run(
        string subjectTowerId,
        IReadOnlyList<Placement> others,
        string creepId,
        int creepCount,
        bool stackedSend,
        string mirrorOf,
        int subjectX = 2,
        int subjectY = 8)
    {
        // mirrorOf is ALWAYS the real tower under test, including on the plain run. Getting this wrong
        // is what makes the whole comparison meaningless: an earlier version mirrored tower.arrow
        // whenever the subject was the plain id, so the "stat-identical" baseline carried Arrow's cost,
        // range, damage and cooldown instead of the subject's. Every number it produced was a
        // comparison between two different towers. Two tests asserting no-op cases caught it.
        var catalog = CatalogMirroring(mirrorOf);
        var service = new CombatService();
        var content = new CombatContent(
            catalog.Creeps,
            catalog.Towers,
            new Dictionary<LaneId, PlayerId> { [Lane] = Defender });
        var routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>> { [Lane] = Route() };
        var definition = catalog.Creeps.Single(creep => creep.Id.Value == creepId);

        var towers = new List<TowerCombatState>
        {
            new(new EntityId(100), new ContentId(subjectTowerId), Defender, Lane, new GridPosition(subjectX, subjectY))
        };
        towers.AddRange(others.Select((placement, index) => new TowerCombatState(
            new EntityId(200 + index),
            new ContentId(placement.TowerId),
            Defender,
            Lane,
            new GridPosition(placement.X, placement.Y))));

        var state = new CombatState(System.Array.Empty<CreepCombatState>(), towers);
        var subjectDamage = 0;
        var otherDamage = 0;
        var targets = new List<long>();
        var nextCreep = 1;

        for (var tick = 0; tick < 160; tick++)
        {
            var seed = new List<CreepCombatState>();
            if (stackedSend)
            {
                if (tick == 0)
                {
                    while (nextCreep <= creepCount)
                    {
                        seed.Add(service.SpawnCreep(new EntityId(nextCreep++), definition, Attacker, Lane));
                    }
                }
            }
            else if (tick % 2 == 0 && nextCreep <= creepCount)
            {
                seed.Add(service.SpawnCreep(new EntityId(nextCreep++), definition, Attacker, Lane));
            }

            if (seed.Count > 0)
            {
                state = new CombatState(state.Creeps.Concat(seed), state.Towers);
            }

            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;

            foreach (var damaged in result.Events.OfType<CreepDamagedEvent>())
            {
                if (damaged.TowerEntityId.Equals(new EntityId(100)))
                {
                    subjectDamage += damaged.DamageDealt;
                }
                else
                {
                    otherDamage += damaged.DamageDealt;
                }
            }

            var fired = result.Events.OfType<TowerFiredEvent>().FirstOrDefault(f => f.TowerEntityId.Equals(new EntityId(100)));
            if (fired is not null)
            {
                targets.Add(fired.TargetCreepEntityId.Value);
            }
        }

        return new Outcome(subjectDamage, otherDamage, subjectDamage + otherDamage, targets);
    }

    private static (Outcome Real, Outcome Plain) Compare(
        string towerId,
        IReadOnlyList<Placement> others,
        string creepId,
        int creepCount,
        bool stackedSend) =>
        (Run(towerId, others, creepId, creepCount, stackedSend, mirrorOf: towerId),
         Run(PlainId, others, creepId, creepCount, stackedSend, mirrorOf: towerId));

    // Grovebond needs Grove neighbours to bond with; they are placed where they cannot reach the lane,
    // so they contribute no damage of their own and cannot flatter the result.
    private static Placement[] GroveNeighbours() => new[]
    {
        new Placement("tower.bloomheart", 1, 8),
        new Placement("tower.thorn_snare", 2, 7),
    };

    [Fact]
    public void Report_every_mechanic_contribution()
    {
        output.WriteLine($"{"mechanic",-34} {"send",-8} {"metric",-10} {"real",6} {"plain",6} {"delta",7}");

        void Line(string name, string send, string metric, int real, int plain)
        {
            var delta = plain == 0 ? 0d : (real - plain) / (double)plain;
            output.WriteLine($"{name,-34} {send,-8} {metric,-10} {real,6} {plain,6} {delta,7:P0}");
        }

        foreach (var stacked in new[] { true, false })
        {
            var send = stacked ? "stacked" : "trickle";

            var grove = Compare("tower.sapling", GroveNeighbours(), "creep.colossus", 8, stacked);
            Line("sapling / Grovebond", send, "self dmg", grove.Real.SubjectDamage, grove.Plain.SubjectDamage);

            var tesla = Compare("tower.tesla", System.Array.Empty<Placement>(), "creep.colossus", 8, stacked);
            Line("tesla / Chain Arc", send, "self dmg", tesla.Real.SubjectDamage, tesla.Plain.SubjectDamage);

            var spore = Compare("tower.spore_cloud", System.Array.Empty<Placement>(), "creep.colossus", 8, stacked);
            Line("spore_cloud / Rot", send, "self dmg", spore.Real.SubjectDamage, spore.Plain.SubjectDamage);

            var bloom = Compare("tower.bloomheart", System.Array.Empty<Placement>(), "creep.colossus", 8, stacked);
            Line("bloomheart / Crowd Bloom", send, "self dmg", bloom.Real.SubjectDamage, bloom.Plain.SubjectDamage);

            var pulse = Compare("tower.pulse", System.Array.Empty<Placement>(), "creep.colossus", 8, stacked);
            Line("pulse / splash (reference)", send, "self dmg", pulse.Real.SubjectDamage, pulse.Plain.SubjectDamage);

            // Servicing shows up on the NEIGHBOUR, so the subject is the drone and the metric is other.
            var drone = Compare("tower.repair_drone", new[] { new Placement("tower.prism", 2, 9) }, "creep.colossus", 8, stacked);
            Line("repair_drone / Servicing", send, "other dmg", drone.Real.OtherTowerDamage, drone.Plain.OtherTowerDamage);

            // Bramble Hold does no damage of its own worth measuring; its value is the extra time it buys
            // every other tower, so total lane damage is the metric.
            var thorn = Compare("tower.thorn_snare", new[] { new Placement("tower.arrow", 4, 8) }, "creep.colossus", 8, stacked);
            Line("thorn_snare / Bramble Hold", send, "lane dmg", thorn.Real.TotalDamage, thorn.Plain.TotalDamage);
        }
    }

    // ---- Grovebond ---------------------------------------------------------------------------

    [Fact]
    public void Grovebond_contributes_in_normal_play()
    {
        var (real, plain) = Compare("tower.sapling", GroveNeighbours(), "creep.colossus", 8, stackedSend: false);

        Assert.True(plain.SubjectDamage > 0, "the sapling never fired, so nothing was measured");
        var gain = (real.SubjectDamage - plain.SubjectDamage) / (double)plain.SubjectDamage;
        Assert.True(gain >= 0.2d, $"Grovebond added {gain:P0} ({real.SubjectDamage} vs {plain.SubjectDamage})");
    }

    [Fact]
    public void Grovebond_contributes_nothing_without_grove_neighbours()
    {
        var (real, plain) = Compare("tower.sapling", System.Array.Empty<Placement>(), "creep.colossus", 8, stackedSend: false);

        Assert.Equal(plain.SubjectDamage, real.SubjectDamage);
    }

    // ---- Chain Arc ---------------------------------------------------------------------------

    /// <summary>
    /// Chain Arc needs a queue behind the leader to jump into. A trickle at one creep every two ticks is
    /// the closest thing to normal play that still forms one.
    /// </summary>
    [Fact]
    public void Chain_arc_contributes_in_normal_play()
    {
        var (real, plain) = Compare("tower.tesla", System.Array.Empty<Placement>(), "creep.colossus", 8, stackedSend: false);

        Assert.True(plain.SubjectDamage > 0, "the tesla never fired, so nothing was measured");
        var gain = (real.SubjectDamage - plain.SubjectDamage) / (double)plain.SubjectDamage;
        Assert.True(gain >= 0.2d, $"Chain Arc added {gain:P0} ({real.SubjectDamage} vs {plain.SubjectDamage})");
    }

    [Fact]
    public void Chain_arc_contributes_nothing_against_a_lone_creep()
    {
        var (real, plain) = Compare("tower.tesla", System.Array.Empty<Placement>(), "creep.colossus", 1, stackedSend: false);

        Assert.Equal(plain.SubjectDamage, real.SubjectDamage);
    }

    // ---- The rest, so a future change cannot quietly make one inert ---------------------------

    [Fact]
    public void Crowd_bloom_contributes_against_a_stacked_send()
    {
        var (real, plain) = Compare("tower.bloomheart", System.Array.Empty<Placement>(), "creep.colossus", 6, stackedSend: true);

        Assert.True(plain.SubjectDamage > 0, "the totem never fired, so nothing was measured");
        Assert.True(real.SubjectDamage > plain.SubjectDamage);
    }

    [Fact]
    public void Rot_contributes_against_a_high_health_creep()
    {
        var (real, plain) = Compare("tower.spore_cloud", System.Array.Empty<Placement>(), "creep.colossus", 8, stackedSend: false);

        Assert.True(plain.SubjectDamage > 0, "the bloom never fired, so nothing was measured");
        var gain = (real.SubjectDamage - plain.SubjectDamage) / (double)plain.SubjectDamage;
        Assert.True(gain >= 0.2d, $"Rot added {gain:P0} ({real.SubjectDamage} vs {plain.SubjectDamage})");
    }

    [Fact]
    public void Servicing_contributes_to_a_slow_neighbour()
    {
        var (real, plain) = Compare(
            "tower.repair_drone",
            new[] { new Placement("tower.prism", 2, 9) },
            "creep.colossus",
            8,
            stackedSend: false);

        Assert.True(plain.OtherTowerDamage > 0, "the neighbour never fired, so nothing was measured");
        var gain = (real.OtherTowerDamage - plain.OtherTowerDamage) / (double)plain.OtherTowerDamage;
        Assert.True(gain >= 0.2d, $"Servicing added {gain:P0} ({real.OtherTowerDamage} vs {plain.OtherTowerDamage})");
    }

    /// <summary>
    /// Bramble Hold's designed case is a burst, so that is where the 20% bar is applied. It measures
    /// +78% against a stacked send and +15% in a trickle.
    /// </summary>
    /// <remarks>
    /// The asymmetry is deliberate rather than a shortfall, and it is the direct result of cutting this
    /// mechanic back: the zone used to cover every route cell the tower could see (5 at range 2), which
    /// made it an automatic purchase. At 3 cells a single creep walking past loses one tick, which is
    /// worth little; a stack of six all lose it at once, which is worth a lot.
    ///
    /// Every mechanic on the roster has a designed case and an off case, so the bar is applied to the
    /// designed one and merely non-negative to the other. Crowd Bloom is +75% stacked and 0% in a
    /// trickle; Servicing is the reverse at 0% and +25%. Demanding 20% everywhere would just be
    /// demanding that every mechanic be unconditional, which is the definition of the mandatory buys we
    /// are trying to avoid.
    /// </remarks>
    [Fact]
    public void Bramble_hold_increases_total_lane_damage_against_a_burst()
    {
        var burst = Compare(
            "tower.thorn_snare",
            new[] { new Placement("tower.arrow", 4, 8) },
            "creep.colossus",
            8,
            stackedSend: true);

        Assert.True(burst.Plain.TotalDamage > 0, "nothing fired, so nothing was measured");
        var gain = (burst.Real.TotalDamage - burst.Plain.TotalDamage) / (double)burst.Plain.TotalDamage;
        Assert.True(gain >= 0.2d, $"Bramble Hold added {gain:P0} against a burst ({burst.Real.TotalDamage} vs {burst.Plain.TotalDamage})");

        // And it must never make things WORSE in the off case.
        var trickle = Compare(
            "tower.thorn_snare",
            new[] { new Placement("tower.arrow", 4, 8) },
            "creep.colossus",
            8,
            stackedSend: false);
        Assert.True(trickle.Real.TotalDamage >= trickle.Plain.TotalDamage);
    }

    // ---- Follow-through: does a strong mechanic make its tower a mandatory buy? ----------------

    /// <summary>
    /// Chain Arc measured at +189% in a trickle, against the Pulse Ward's +33% from splash — the
    /// strongest contribution of any mechanic relative to its own baseline. That raises the opposite
    /// question to inertness, so it gets the opportunity-cost check: does spending 38 gold on a Tesla
    /// beat spending comparable gold on plain damage?
    /// </summary>
    [Fact]
    public void Tesla_does_not_beat_equal_gold_spent_on_plain_damage()
    {
        var catalog = SampleVerticalSliceContent.Create();

        double PerGold(params Placement[] placements)
        {
            var gold = placements.Sum(placement =>
                catalog.Towers.Single(tower => tower.Id.Value == placement.TowerId).Cost.Amount);
            // The subject slot is unused here, so put the whole bundle in `others` and read lane damage.
            var outcome = Run(placements[0].TowerId, placements.Skip(1).ToArray(), "creep.colossus", 8, stackedSend: false,
                mirrorOf: placements[0].TowerId, subjectX: placements[0].X, subjectY: placements[0].Y);
            return outcome.TotalDamage / (double)gold;
        }

        var tesla = PerGold(new Placement("tower.tesla", 2, 8));
        var arrows = PerGold(new Placement("tower.arrow", 2, 8), new Placement("tower.arrow", 2, 10), new Placement("tower.arrow", 1, 8));

        Assert.True(
            arrows >= tesla,
            $"a Tesla returns {tesla:F2} damage per gold against {arrows:F2} for plain damage, so it is a mandatory pick");
    }

    /// <summary>
    /// Servicing measured 0% against a stacked send and +25% in a trickle. That asymmetry is recorded
    /// rather than fixed: a cadence buff only pays out over sustained pressure, because a stack passes
    /// through a slow tower's range inside a single cooldown either way. It is a real limitation of the
    /// tower, not a bug, and it is the honest counterpart to the fact that Crowd Bloom only pays out
    /// against a stack.
    /// </summary>
    [Fact]
    public void Servicing_is_worth_nothing_against_a_single_stacked_burst()
    {
        var (real, plain) = Compare(
            "tower.repair_drone",
            new[] { new Placement("tower.prism", 2, 9) },
            "creep.colossus",
            6,
            stackedSend: true);

        Assert.Equal(plain.OtherTowerDamage, real.OtherTowerDamage);
    }

    // ---- Deep Roots: the last mechanic keyed on strict creep ordering -------------------------

    /// <summary>
    /// Elder Canopy orders candidates by ascending path index to shoot the creep furthest back. That is
    /// the same SHAPE as the two rules that already failed — a comparison over creep ordering — so it
    /// gets checked rather than assumed.
    /// </summary>
    /// <remarks>
    /// A selection rule's contribution is not damage, it is which creep gets shot, so this compares
    /// target sequences rather than totals. Against a stacked send every index ties and the rule can only
    /// fall through to entity id, exactly as Reaping Bloom did; against a trickle the indices genuinely
    /// differ and it should diverge from the default front-most rule on nearly every shot.
    /// </remarks>
    [Fact]
    public void Deep_roots_changes_the_target_when_creeps_are_spread_out()
    {
        var (real, plain) = Compare("tower.elder_canopy", System.Array.Empty<Placement>(), "creep.colossus", 8, stackedSend: false);

        var shots = System.Math.Min(real.Targets.Count, plain.Targets.Count);
        Assert.True(shots > 0, "the canopy never fired, so nothing was measured");

        var diverged = Enumerable.Range(0, shots).Count(index => real.Targets[index] != plain.Targets[index]);
        Assert.True(
            diverged > 0,
            $"Deep Roots picked the same creep as the default rule on all {shots} shots — the mechanic is inert");
    }

    [Fact]
    public void Report_deep_roots_target_divergence()
    {
        foreach (var stacked in new[] { true, false })
        {
            var (real, plain) = Compare("tower.elder_canopy", System.Array.Empty<Placement>(), "creep.colossus", 8, stacked);
            var shots = System.Math.Min(real.Targets.Count, plain.Targets.Count);
            var diverged = Enumerable.Range(0, shots).Count(index => real.Targets[index] != plain.Targets[index]);
            var rate = shots == 0 ? 0d : diverged / (double)shots;
            output.WriteLine($"elder_canopy / Deep Roots  {(stacked ? "stacked" : "trickle"),-8} shots={shots,3} diverged={diverged,3} {rate,6:P0}");
        }
    }
}
