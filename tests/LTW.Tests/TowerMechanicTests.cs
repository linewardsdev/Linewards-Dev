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

    /// <summary>
    /// A creep parked on <paramref name="pathIndex"/> and primed so the next unbraked tick advances
    /// it exactly one cell.
    /// </summary>
    /// <remarks>
    /// These tests are about what a tower does to a creep at a given place, and were written when a
    /// creep covered one cell per tick, so "put it here, advance once, it is one further" was free.
    /// CombatService.BaseMovementCost makes a cell cost three ticks, so banking cost-minus-speed
    /// here restores that property instead of scattering tick counts through every test.
    /// </remarks>
    private static CreepCombatState CreepAt(CombatService service, int entityId, string creepId, int pathIndex)
    {
        var spawned = service.SpawnCreep(new EntityId(entityId), Creep(creepId), Attacker, Lane);
        return spawned.WithMovement(pathIndex, PrimedMovement(creepId));
    }

    private static int PrimedMovement(string creepId) =>
        CombatService.BaseMovementCost - Creep(creepId).SpeedPerSecond;

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

    // ---- Bloomheart: Crowd Bloom --------------------------------------------------------------

    [Fact]
    public void Bloomheart_deals_its_authored_damage_against_a_lone_creep()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 8) },
            new[] { Tower("tower.bloomheart", 10, x: 2, y: 8) });

        Assert.Equal(4, DamageFrom(service, state));
    }

    [Fact]
    public void Bloomheart_gains_damage_for_each_creep_sharing_the_targets_cell()
    {
        var service = new CombatService();
        // Three creeps stacked on one cell, which is what a quantity-N send looks like.
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.colossus", pathIndex: 8),
                CreepAt(service, 2, "creep.colossus", pathIndex: 8),
                CreepAt(service, 3, "creep.colossus", pathIndex: 8)
            },
            new[] { Tower("tower.bloomheart", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var damage = result.Events.OfType<CreepDamagedEvent>().Single().DamageDealt;

        // 4 authored + 2 others on the cell.
        Assert.Equal(6, damage);
    }

    [Fact]
    public void Crowd_bloom_bonus_is_capped()
    {
        var service = new CombatService();
        var creeps = Enumerable.Range(1, 8)
            .Select(index => CreepAt(service, index, "creep.colossus", pathIndex: 8))
            .ToArray();
        var state = new CombatState(creeps, new[] { Tower("tower.bloomheart", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var damage = result.Events.OfType<CreepDamagedEvent>().Single().DamageDealt;

        // 4 authored + 3 cap, not + 7.
        Assert.Equal(7, damage);
    }

    [Fact]
    public void Crowd_bloom_ignores_creeps_on_other_cells()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.colossus", pathIndex: 8),
                CreepAt(service, 2, "creep.colossus", pathIndex: 7),
                CreepAt(service, 3, "creep.colossus", pathIndex: 6)
            },
            new[] { Tower("tower.bloomheart", 10, x: 2, y: 8) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var primary = result.Events.OfType<CreepDamagedEvent>()
            .OrderByDescending(damaged => damaged.DamageDealt)
            .First();

        // Spread out in single file, so no crowd bonus at all.
        Assert.Equal(4, primary.DamageDealt);
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

    /// <summary>
    /// A route can pass a single Thorn Snare twice — near it, away, then back — which is normal on a
    /// mazed lane. BrambleZonesFor must brake each visit as its own span rather than collapsing the
    /// first and last covered indices into one span that also brakes the stretch in between where
    /// the tower cannot actually reach (OPEN_ITEMS.md item 20).
    /// </summary>
    [Fact]
    public void Bramble_does_not_brake_a_stretch_the_tower_cannot_reach_between_two_visits()
    {
        var service = new CombatService();
        var content = Content();
        // Thorn Snare's real range is 2 (SampleVerticalSliceContent). Index 0 (5,4) and index 5
        // (5,6) are both distance 1 from a tower at (5,5) — in range. Indices 1-4 detour to x=0,
        // all distance >= 6 — nowhere near in range, unlike a naive "only check the endpoints"
        // route where a wider minimum-width span could still reach a middle index by accident.
        var routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>
        {
            [Lane] = new[]
            {
                new GridPosition(5, 4),
                new GridPosition(0, 4),
                new GridPosition(0, 3),
                new GridPosition(0, 2),
                new GridPosition(0, 1),
                new GridPosition(5, 6)
            }
        };
        var towers = new[] { Tower("tower.thorn_snare", 10, x: 5, y: 5) };
        // Colossus: speed 1 and 90 max health, so a single Thorn Snare hit (5 damage) cannot kill it
        // and the only thing this test measures is movement. Primed with cost-minus-speed banked, so
        // one unbraked tick affords exactly one step while a braked one (double cost) still cannot —
        // which is the whole distinction being tested. Starting at index 3, the middle of the
        // unreachable detour, is the case the old single-span bug got wrong.
        var creep = service.SpawnCreep(new EntityId(1), Creep("creep.colossus"), Attacker, Lane)
            .WithMovement(pathIndex: 3, movementProgress: PrimedMovement("creep.colossus"));
        var state = new CombatState(new[] { creep }, towers);

        var result = service.Advance(state, content, routes, new SimulationTick(0));

        var moved = result.State.Creeps.Single();
        Assert.Equal(4, moved.PathIndex);
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

    // ---- Tesla: Chain Arc ---------------------------------------------------------------------

    [Fact]
    public void Tesla_chains_forward_through_a_line_of_creeps_with_decaying_damage()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.colossus", pathIndex: 7),
                CreepAt(service, 2, "creep.colossus", pathIndex: 8),
                CreepAt(service, 3, "creep.colossus", pathIndex: 9)
            },
            new[] { Tower("tower.tesla", 10, x: 2, y: 9) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var byCreep = result.Events.OfType<CreepDamagedEvent>()
            .ToDictionary(damaged => damaged.CreepEntityId.Value, damaged => damaged.DamageDealt);

        // Entity 3 is the front-most and takes the primary 5; the arc then jumps BACK through the
        // queue, halving: entity 2 takes 2, entity 1 takes 1.
        Assert.Equal(3, byCreep.Count);
        Assert.Equal(5, byCreep[3]);
        Assert.Equal(2, byCreep[2]);
        Assert.Equal(1, byCreep[1]);
    }

    [Fact]
    public void Tesla_chain_stops_when_the_queue_has_a_gap_wider_than_a_hop()
    {
        var service = new CombatService();
        // Entity 2 sits 4 route cells behind the leader, further than ChainArcHopRangeCells, so the
        // arc has nowhere to jump and only the primary is hit.
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.colossus", pathIndex: 8),
                CreepAt(service, 2, "creep.colossus", pathIndex: 3)
            },
            new[] { Tower("tower.tesla", 10, x: 2, y: 9) });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var hit = result.Events.OfType<CreepDamagedEvent>().Select(d => d.CreepEntityId.Value).ToArray();

        Assert.Equal(new long[] { 1 }, hit);
    }

    [Fact]
    public void Tesla_alone_deals_only_its_own_damage()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 8) },
            new[] { Tower("tower.tesla", 10, x: 2, y: 9) });

        Assert.Equal(5, DamageFrom(service, state));
    }

    // ---- Repair Drone Spire: cooldown servicing ----------------------------------------------

    [Fact]
    public void Repair_drone_makes_an_adjacent_tower_fire_faster()
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();

        int ShotsOverTicks(bool withDrone)
        {
            var towers = new List<TowerCombatState> { Tower("tower.arrow", 10, x: 2, y: 8) };
            if (withDrone)
            {
                towers.Add(Tower("tower.repair_drone", 11, x: 1, y: 8));
            }

            // One creep with enough health to survive the whole window, parked in range by respawning
            // it each tick so only cadence is measured, not target availability.
            var shots = 0;
            var state = new CombatState(
                new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 6) },
                towers);

            for (var tick = 0; tick < 6; tick++)
            {
                var result = service.Advance(state, content, routes, new SimulationTick(tick));
                state = result.State;
                shots += result.Events.OfType<TowerFiredEvent>().Count(f => f.TowerEntityId.Equals(new EntityId(10)));
            }

            return shots;
        }

        var unaided = ShotsOverTicks(withDrone: false);
        var serviced = ShotsOverTicks(withDrone: true);

        Assert.True(unaided > 0, "the Arrow never fired, so nothing was measured");
        Assert.True(serviced > unaided, $"serviced Arrow fired {serviced} times against {unaided} unaided");
    }

    [Fact]
    public void Repair_drone_cooldown_bonus_does_not_stack()
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();

        int ShotsWith(int droneCount)
        {
            var towers = new List<TowerCombatState> { Tower("tower.prism", 10, x: 2, y: 8) };
            if (droneCount >= 1)
            {
                towers.Add(Tower("tower.repair_drone", 11, x: 1, y: 8));
            }

            if (droneCount >= 2)
            {
                towers.Add(Tower("tower.repair_drone", 12, x: 3, y: 8));
            }

            var shots = 0;
            var state = new CombatState(new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 6) }, towers);
            for (var tick = 0; tick < 12; tick++)
            {
                var result = service.Advance(state, content, routes, new SimulationTick(tick));
                state = result.State;
                shots += result.Events.OfType<TowerFiredEvent>().Count(f => f.TowerEntityId.Equals(new EntityId(10)));
            }

            return shots;
        }

        // Prism's cooldown is 6, so one drone takes it to 5 and two must not take it to 4.
        Assert.Equal(ShotsWith(1), ShotsWith(2));
    }

    /// <summary>
    /// A tower already firing every tick has nothing to gain, so the drone is not universally useful.
    /// </summary>
    [Fact]
    public void Repair_drone_does_nothing_for_a_tower_already_at_minimum_cooldown()
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();

        int ShotsWith(bool withDrone)
        {
            var towers = new List<TowerCombatState> { Tower("tower.gatling", 10, x: 2, y: 8) };
            if (withDrone)
            {
                towers.Add(Tower("tower.repair_drone", 11, x: 1, y: 8));
            }

            var shots = 0;
            var state = new CombatState(new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 6) }, towers);
            for (var tick = 0; tick < 6; tick++)
            {
                var result = service.Advance(state, content, routes, new SimulationTick(tick));
                state = result.State;
                shots += result.Events.OfType<TowerFiredEvent>().Count(f => f.TowerEntityId.Equals(new EntityId(10)));
            }

            return shots;
        }

        Assert.Equal(ShotsWith(withDrone: false), ShotsWith(withDrone: true));
    }

    // ---- Elder Canopy: back-most targeting ---------------------------------------------------

    [Fact]
    public void Elder_canopy_shoots_the_creep_furthest_back_in_range()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.brute", pathIndex: 10),
                CreepAt(service, 2, "creep.brute", pathIndex: 5)
            },
            new[] { Tower("tower.elder_canopy", 10, x: 2, y: 8) });

        var fired = service.Advance(state, Content(), Routes(), new SimulationTick(0))
            .Events.OfType<TowerFiredEvent>().Single();

        Assert.Equal(new EntityId(2), fired.TargetCreepEntityId);
    }

    [Fact]
    public void Other_long_range_towers_still_shoot_the_leader()
    {
        var service = new CombatService();
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.brute", pathIndex: 10),
                CreepAt(service, 2, "creep.brute", pathIndex: 5)
            },
            new[] { Tower("tower.prism", 10, x: 2, y: 8) });

        var fired = service.Advance(state, Content(), Routes(), new SimulationTick(0))
            .Events.OfType<TowerFiredEvent>().Single();

        Assert.Equal(new EntityId(1), fired.TargetCreepEntityId);
    }

    // ---- Scaling shape: every mechanic must be proportional to base damage -------------------

    /// <summary>
    /// Rot is a multiple of base damage, not a floor over a fixed health fraction.
    /// </summary>
    /// <remarks>
    /// The distinction only shows up once a tower-line tier scales base damage, which is exactly why it is
    /// pinned now rather than discovered then. Under the old form the rot term ignored base entirely, so
    /// upgrading the GROVE line would have done nothing for this tower against the fat targets it exists to
    /// answer. Asserting the RATIO rather than absolute numbers is what makes this a scaling test.
    /// </remarks>
    [Theory]
    [InlineData("creep.brute", 100)]          // 24 max health: exactly one multiple, so base
    [InlineData("creep.siege", 200)]          // 48 max health: two multiples
    [InlineData("creep.obsidian_brute", 250)] // 60 max health
    [InlineData("creep.colossus", 375)]       // 90 max health
    public void Rot_is_a_fixed_multiple_of_base_damage_per_target(string creepId, int expectedPercentOfBase)
    {
        var service = new CombatService();
        var state = new CombatState(
            new[] { CreepAt(service, 1, creepId, pathIndex: 8) },
            new[] { Tower("tower.spore_cloud", 10, x: 2, y: 8) });

        var authored = Catalog.Towers.Single(tower => tower.Id.Value == "tower.spore_cloud").Damage;
        var dealt = DamageFrom(service, state);

        Assert.Equal(authored * expectedPercentOfBase / 100, dealt);
    }

    /// <summary>
    /// Grovebond and Crowd Bloom are percentages of base, so their bonus is a fixed RATIO of the tower's
    /// damage rather than a flat number that would shrink against a scaled base.
    /// </summary>
    [Fact]
    public void Grovebond_bonus_is_a_ratio_of_base_damage()
    {
        var service = new CombatService();
        var authored = Catalog.Towers.Single(tower => tower.Id.Value == "tower.sapling").Damage;
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 8) },
            new[]
            {
                Tower("tower.sapling", 10, x: 2, y: 8),
                Tower("tower.bloomheart", 11, x: 1, y: 8),
                Tower("tower.thorn_snare", 12, x: 2, y: 7)
            });

        var result = service.Advance(state, Content(), Routes(), new SimulationTick(0));
        var saplingDamage = result.Events.OfType<CreepDamagedEvent>()
            .Where(damaged => damaged.TowerEntityId.Equals(new EntityId(10)))
            .Sum(damaged => damaged.DamageDealt);

        // Two bonded neighbours at 50% of base each: base + base.
        Assert.Equal(authored * 200 / 100, saplingDamage);
    }

    [Fact]
    public void Crowd_bloom_bonus_is_a_ratio_of_base_damage()
    {
        var service = new CombatService();
        var authored = Catalog.Towers.Single(tower => tower.Id.Value == "tower.bloomheart").Damage;
        var creeps = Enumerable.Range(1, 4)
            .Select(index => CreepAt(service, index, "creep.colossus", pathIndex: 8))
            .ToArray();
        var state = new CombatState(creeps, new[] { Tower("tower.bloomheart", 10, x: 2, y: 8) });

        var dealt = service.Advance(state, Content(), Routes(), new SimulationTick(0))
            .Events.OfType<CreepDamagedEvent>().Single().DamageDealt;

        // Three others on the cell at 25% of base each: base + 75%.
        Assert.Equal(authored * 175 / 100, dealt);
    }

    /// <summary>
    /// Pulse's splash is half of BASE damage, so it scales with the tower rather than sitting at the
    /// authored value forever.
    /// </summary>
    [Fact]
    public void Pulse_splash_is_half_of_base_damage()
    {
        var service = new CombatService();
        var authored = Catalog.Towers.Single(tower => tower.Id.Value == "tower.pulse").Damage;
        // Pulse has range 1, so the creeps must land LEVEL with the tower (route row 8), not one past it.
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.colossus", pathIndex: 7),
                CreepAt(service, 2, "creep.colossus", pathIndex: 7)
            },
            new[] { Tower("tower.pulse", 10, x: 2, y: 8) });

        var damages = service.Advance(state, Content(), Routes(), new SimulationTick(0))
            .Events.OfType<CreepDamagedEvent>()
            .Select(damaged => damaged.DamageDealt)
            .OrderByDescending(value => value)
            .ToArray();

        Assert.Equal(authored, damages[0]);
        Assert.Equal(authored / 2, damages[1]);
    }

    /// <summary>
    /// Chain Arc's hops halve from BASE damage. Proportional by construction, so it already scales — this
    /// pins that the chain reads base rather than the primary hit's post-mechanic damage.
    /// </summary>
    [Fact]
    public void Chain_arc_hops_halve_from_base_damage()
    {
        var service = new CombatService();
        var authored = Catalog.Towers.Single(tower => tower.Id.Value == "tower.tesla").Damage;
        var state = new CombatState(
            new[]
            {
                CreepAt(service, 1, "creep.colossus", pathIndex: 7),
                CreepAt(service, 2, "creep.colossus", pathIndex: 8),
                CreepAt(service, 3, "creep.colossus", pathIndex: 9)
            },
            new[] { Tower("tower.tesla", 10, x: 2, y: 9) });

        var byCreep = service.Advance(state, Content(), Routes(), new SimulationTick(0))
            .Events.OfType<CreepDamagedEvent>()
            .ToDictionary(damaged => damaged.CreepEntityId.Value, damaged => damaged.DamageDealt);

        Assert.Equal(authored, byCreep[3]);
        Assert.Equal(authored / 2, byCreep[2]);
        Assert.Equal(authored / 2 / 2, byCreep[1]);
    }

    /// <summary>
    /// The Foundry's shell reads base damage too. It resolves in a separate phase where the shot's local is
    /// out of scope, which made it the easiest of the four damage paths to leave behind.
    /// </summary>
    [Fact]
    public void Foundry_shell_deals_base_damage()
    {
        var service = new CombatService();
        var content = Content();
        var routes = Routes();
        var authored = Catalog.Towers.Single(tower => tower.Id.Value == "tower.foundry").Damage;
        var state = new CombatState(
            new[] { CreepAt(service, 1, "creep.colossus", pathIndex: 8) },
            new[] { Tower("tower.foundry", 10, x: 2, y: 8) });

        var dealt = 0;
        for (var tick = 0; tick <= 4 && dealt == 0; tick++)
        {
            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            state = result.State;
            dealt = result.Events.OfType<CreepDamagedEvent>().Sum(damaged => damaged.DamageDealt);
        }

        Assert.Equal(authored, dealt);
    }
}
