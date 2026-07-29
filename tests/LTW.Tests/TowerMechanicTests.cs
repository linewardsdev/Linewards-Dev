using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using Xunit;

namespace LTW.Tests;

/// <summary>
/// Pins the per-tower mechanics: Barricade's fixed arc, Grovebond, Rot, Reaping Bloom, Bramble Hold
/// and the Stack Mortar.
/// </summary>
public sealed class TowerMechanicTests
{
    private static readonly LaneId Lane = new(1);
    private static readonly PlayerId Defender = new(1);
    private static readonly PlayerId Attacker = new(2);

    private static readonly ContentCatalog Catalog = SampleVerticalSliceContent.Create();

    /// <summary>Straight column at x=3, matching the real map's lane.</summary>
    private static IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> Routes() =>
        new Dictionary<LaneId, IReadOnlyList<GridPosition>>
        {
            [Lane] = Enumerable.Range(0, 18).Select(y => new GridPosition(3, y)).ToArray()
        };

    private static CombatContent Content() => new(
        Catalog.Creeps,
        Catalog.Towers,
        new Dictionary<LaneId, PlayerId> { [Lane] = Defender });

    private static CreepDefinition Creep(string id) => Catalog.Creeps.First(creep => creep.Id.Value == id);

    private static TowerCombatState Tower(string towerId, int entityId, int x, int y) =>
        new(new EntityId(entityId), new ContentId(towerId), Defender, Lane, new GridPosition(x, y));

    private static CreepCombatState CreepAt(CombatService service, int entityId, string creepId, int pathIndex)
    {
        var spawned = service.SpawnCreep(new EntityId(entityId), Creep(creepId), Attacker, Lane);
        return spawned.WithMovement(pathIndex, 0);
    }

    // ---- Barricade: fixed up-lane arc ---------------------------------------------------------

    [Fact]
    public void Barricade_engages_a_creep_that_has_not_passed_its_row()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.runner", pathIndex: 7) },
            new[] { Tower("tower.barricade", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));

        Assert.Single(result.Events.OfType<TowerFiredEvent>());
    }

    [Fact]
    public void Barricade_will_not_engage_a_creep_that_has_walked_past_it()
    {
        var service = new CombatService();
        // Lands on route row 9: one row BEYOND the tower at y=8 but still inside range 2, so the
        // arc is the only thing that can stop the shot. Placed at 9 it would land on 10 and be out
        // of range anyway, and the test would pass for the wrong reason.
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.runner", pathIndex: 8) },
            new[] { Tower("tower.barricade", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));

        Assert.Empty(result.Events.OfType<TowerFiredEvent>());
    }

    /// <summary>
    /// The restriction has to apply to the aim snapshot too, or the turret visibly tracks a creep it
    /// will never shoot.
    /// </summary>
    [Fact]
    public void Barricade_does_not_aim_at_a_creep_behind_it()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.runner", pathIndex: 10) },
            new[] { Tower("tower.barricade", 10, x: 2, y: 8) });

        Assert.Empty(service.GetTowerAimSnapshots(state, Content(), Routes()));
    }

    [Fact]
    public void Other_towers_still_engage_creeps_behind_them()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.runner", pathIndex: 8) },
            new[] { Tower("tower.arrow", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));

        Assert.Single(result.Events.OfType<TowerFiredEvent>());
    }

    // ---- Sapling: Grovebond ------------------------------------------------------------------

    private static int DamageFrom(CombatService service, CombatState state)
    {
        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        return result.Events.OfType<CreepDamagedEvent>().Sum(damaged => damaged.DamageDealt);
    }

    [Fact]
    public void Lone_sapling_deals_its_authored_damage()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.brute", pathIndex: 8) },
            new[] { Tower("tower.sapling", 10, x: 2, y: 8) });

        Assert.Equal(2, DamageFrom(service, state));
    }

    [Fact]
    public void Sapling_gains_one_damage_per_orthogonally_adjacent_grove_tower()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.brute", pathIndex: 8) },
            new[]
            {
                Tower("tower.sapling", 10, x: 2, y: 8),
                Tower("tower.bloomheart", 11, x: 1, y: 8),
                Tower("tower.thorn_snare", 12, x: 2, y: 7)
            });

        // 2 authored + 2 adjacent Grove towers. Only the sapling's own shot is counted; the other
        // two are placed where they cannot also reach the creep.
        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var saplingDamage = result.Events.OfType<CreepDamagedEvent>()
            .Where(damaged => damaged.TowerEntityId.Equals(new EntityId(10)))
            .Sum(damaged => damaged.DamageDealt);

        Assert.Equal(4, saplingDamage);
    }

    [Fact]
    public void Grovebond_ignores_diagonal_neighbours_and_non_grove_towers()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.brute", pathIndex: 8) },
            new[]
            {
                Tower("tower.sapling", 10, x: 2, y: 8),
                Tower("tower.bloomheart", 11, x: 1, y: 7),   // diagonal — must not bond
                Tower("tower.gatling", 12, x: 2, y: 7)       // adjacent but not Grove
            });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var saplingDamage = result.Events.OfType<CreepDamagedEvent>()
            .Where(damaged => damaged.TowerEntityId.Equals(new EntityId(10)))
            .Sum(damaged => damaged.DamageDealt);

        Assert.Equal(2, saplingDamage);
    }

    // ---- Spore Cloud: Rot --------------------------------------------------------------------

    [Theory]
    [InlineData("creep.runner", 4)]        // 10 max health, below the step: floors at authored 4
    [InlineData("creep.serpent", 5)]       // 32 / 6 = 5
    [InlineData("creep.obsidian_brute", 10)] // 60 / 6 = 10
    public void Rot_scales_with_the_targets_authored_max_health(string creepId, int expected)
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, creepId, pathIndex: 8) },
            new[] { Tower("tower.spore_cloud", 10, x: 2, y: 8) });

        Assert.Equal(expected, DamageFrom(service, state));
    }

    /// <summary>
    /// Rot reads AUTHORED max health, so chipping a creep first cannot inflate the hit.
    /// </summary>
    [Fact]
    public void Rot_is_not_affected_by_the_targets_current_health()
    {
        var service = new CombatService();
        var wounded = CreepAt(service, 1, "creep.obsidian_brute", pathIndex: 8).WithHealth(3);
        var state = new CombatState(new[] { wounded }, new[] { Tower("tower.spore_cloud", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));

        Assert.Equal(10, result.Events.OfType<CreepDamagedEvent>().Single().DamageDealt);
    }

    // ---- Bloomheart: Reaping Bloom -----------------------------------------------------------

    [Fact]
    public void Bloomheart_finishes_a_killable_creep_instead_of_the_leader()
    {
        var service = new CombatService();
        var leader = CreepAt(service, 1, "creep.brute", pathIndex: 8);
        var woundedTrailer = CreepAt(service, 2, "creep.brute", pathIndex: 6).WithHealth(2);
        var state = new CombatState(
            new[] { leader, woundedTrailer },
            new[] { Tower("tower.bloomheart", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));

        var killed = Assert.Single(result.Events.OfType<CreepKilledEvent>());
        Assert.Equal(new EntityId(2), killed.CreepEntityId);
    }

    [Fact]
    public void Bloomheart_falls_back_to_the_leader_when_nothing_is_killable()
    {
        var service = new CombatService();
        var leader = CreepAt(service, 1, "creep.brute", pathIndex: 8);
        var trailer = CreepAt(service, 2, "creep.brute", pathIndex: 6);
        var state = new CombatState(
            new[] { leader, trailer },
            new[] { Tower("tower.bloomheart", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));

        var fired = Assert.Single(result.Events.OfType<TowerFiredEvent>());
        Assert.Equal(new EntityId(1), fired.TargetCreepEntityId);
    }

    // ---- Thorn Snare: Bramble Hold -----------------------------------------------------------

    [Fact]
    public void Bramble_halves_movement_through_the_zone()
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();

        int TransitTicks(bool withThorn)
        {
            var towers = withThorn
                ? new[] { Tower("tower.thorn_snare", 10, x: 2, y: 8) }
                : System.Array.Empty<TowerCombatState>();
            // Health high enough that the thorn cannot kill it, so only movement is measured.
            var creep = service.SpawnCreep(new EntityId(1), Creep("creep.colossus"), Attacker, Lane);
            var state = new CombatState(new[] { creep }, towers);

            for (var tick = 0; tick < 200; tick++)
            {
                var result = service.Advance(state, content, routes, new SimulationTick(tick));
                state = result.State;
                if (result.Events.OfType<LeakEvent>().Any())
                {
                    return tick + 1;
                }
            }

            return -1;
        }

        var clean = TransitTicks(withThorn: false);
        var braked = TransitTicks(withThorn: true);

        Assert.True(clean > 0 && braked > 0, $"transit never completed (clean={clean}, braked={braked})");
        Assert.True(braked > clean, $"bramble did not slow the creep (clean={clean}, braked={braked})");
    }

    // ---- Foundry: Stack Mortar ---------------------------------------------------------------

    [Fact]
    public void Foundry_deals_no_damage_on_the_tick_it_fires()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 8) },
            new[] { Tower("tower.foundry", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));

        Assert.Single(result.Events.OfType<TowerFiredEvent>());
        Assert.Empty(result.Events.OfType<CreepDamagedEvent>());
        Assert.True(result.State.Towers.Single().HasShellInFlight);
    }

    [Fact]
    public void Foundry_shell_lands_after_its_flight_time_and_leads_the_target()
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 7) },
            new[] { Tower("tower.foundry", 10, x: 2, y: 8) });

        var launch = service.Advance(state, content, routes, new SimulationTick(0));
        state = launch.State;
        var fired = launch.Events.OfType<TowerFiredEvent>().Single();

        var damageTick = -1;
        for (var tick = 1; tick <= 4; tick++)
        {
            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;
            if (result.Events.OfType<CreepDamagedEvent>().Any())
            {
                damageTick = tick;
                break;
            }
        }

        // The event advertises where and when, and the shell actually lands there.
        Assert.Equal(fired.ImpactTick.Value, damageTick);
        Assert.False(state.Towers.Single().HasShellInFlight);
    }

    /// <summary>
    /// Arming the shell and setting the cooldown in the same tick must not disarm the shell. This is
    /// the failure the WithNextAttackTick copy exists to prevent, and it would compile silently.
    /// </summary>
    [Fact]
    public void Setting_the_cooldown_preserves_a_shell_in_flight()
    {
        var tower = Tower("tower.foundry", 10, x: 2, y: 8)
            .WithShellInFlight(new SimulationTick(5), new GridPosition(3, 9))
            .WithNextAttackTick(new SimulationTick(6));

        Assert.True(tower.HasShellInFlight);
        Assert.Equal(new GridPosition(3, 9), tower.ShellImpactCell);
        Assert.Equal(5, tower.ShellImpactTick!.Value.Value);
    }

    [Fact]
    public void Foundry_does_not_fire_again_while_a_shell_is_in_flight()
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 6) },
            new[] { Tower("tower.foundry", 10, x: 2, y: 8) });

        var launch = service.Advance(state, content, routes, new SimulationTick(0));
        state = launch.State;
        var second = service.Advance(state, content, routes, new SimulationTick(1));

        Assert.Empty(second.Events.OfType<TowerFiredEvent>());
    }

    /// <summary>
    /// Direct-fire towers report an impact of "here, now", so the renderer can treat every shot
    /// uniformly instead of special-casing the mortar.
    /// </summary>
    [Fact]
    public void Direct_fire_towers_report_impact_as_this_tick_at_the_target()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.brute", pathIndex: 8) },
            new[] { Tower("tower.arrow", 10, x: 2, y: 8) });

        var fired = service.Advance(state, Content(), Routes(), new SimulationTick(3))
            .Events.OfType<TowerFiredEvent>().Single();

        Assert.Equal(3, fired.ImpactTick.Value);
        Assert.Equal(fired.TargetPosition, fired.ImpactPosition);
    }
}
