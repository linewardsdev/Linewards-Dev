using LTW.Simulation.Combat;
using LTW.Simulation.Content;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

public sealed class CombatTests
{
    [Fact]
    public void Tower_damages_and_kills_creep_within_expected_ticks()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes();
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne) },
            new[] { new TowerCombatState(new EntityId(10), ArrowTowerId, new PlayerId(1), LaneOne, new GridPosition(1, 1)) });

        state = service.Advance(state, content, routes, new SimulationTick(0)).State;
        var result = service.Advance(state, content, routes, new SimulationTick(2));

        Assert.Empty(result.State.Creeps);
        Assert.Contains(result.Events, simulationEvent => simulationEvent is CreepKilledEvent killed && killed.CreepEntityId.Equals(new EntityId(1)));
    }

    [Fact]
    public void Tower_firing_emits_exactly_one_TowerFiredEvent_per_attack()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes();
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne) },
            new[] { new TowerCombatState(new EntityId(10), ArrowTowerId, new PlayerId(1), LaneOne, new GridPosition(1, 1)) });

        var result = service.Advance(state, content, routes, new SimulationTick(0));

        var fired = Assert.Single(result.Events.OfType<TowerFiredEvent>());
        Assert.Equal(new EntityId(10), fired.TowerEntityId);
        Assert.Equal(new EntityId(1), fired.TargetCreepEntityId);
        Assert.Equal(LaneOne, fired.LaneId);
    }

    [Fact]
    public void Tower_vision_extends_beyond_attack_range_without_firing()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes(length: 10);
        var creep = service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne).WithMovement(4, 0);
        var state = new CombatState(
            new[] { creep },
            new[] { new TowerCombatState(new EntityId(10), ArrowTowerId, new PlayerId(1), LaneOne, new GridPosition(1, 1)) });

        // Distance 3 from the tower: outside Arrow's 2-cell attack range, so it must not fire...
        var result = service.Advance(state, content, routes, new SimulationTick(0));
        Assert.Empty(result.Events.OfType<TowerFiredEvent>());

        // ...but within its extended vision (2 + VisionBufferCells), so the turret should already
        // be tracking it well before it's actually in range to shoot.
        var aimSnapshots = service.GetTowerAimSnapshots(state, content, routes);
        var aim = Assert.Single(aimSnapshots);
        Assert.Equal(new EntityId(10), aim.TowerEntityId);
        Assert.Equal(new GridPosition(4, 1), aim.TargetPosition);
    }

    [Fact]
    public void Creep_reaching_exit_emits_one_leak_event_only()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes(length: 2);
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne) },
            Array.Empty<TowerCombatState>());

        var first = AdvanceUntilLeak(service, state, content, routes);
        var second = service.Advance(first.State, content, routes, new SimulationTick(99));

        Assert.Single(first.Events.OfType<LeakEvent>());
        Assert.Empty(second.Events.OfType<LeakEvent>());
    }

    [Fact]
    public void Movement_and_combat_reproduce_for_same_sequence()
    {
        static (int Creeps, int Events, int SnapshotX) Run()
        {
            var service = new CombatService();
            var content = CreateContent();
            var routes = CreateRoutes();
            var state = new CombatState(
                new[] { service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne) },
                new[] { new TowerCombatState(new EntityId(10), ArrowTowerId, new PlayerId(1), LaneOne, new GridPosition(1, 1)) });
            var events = 0;

            for (var tick = 0; tick < 3; tick++)
            {
                var result = service.Advance(state, content, routes, new SimulationTick(tick));
                state = result.State;
                events += result.Events.Count;
            }

            var snapshot = service.GetCreepSnapshots(state, content, routes).FirstOrDefault();
            return (state.Creeps.Count, events, snapshot?.Position.X ?? -1);
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Presentation_snapshots_include_lightweight_creep_state()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes();
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne) },
            Array.Empty<TowerCombatState>());

        // Advances until the creep actually steps a cell rather than assuming one tick does it: a
        // cell now costs CombatService.BaseMovementCost ticks of banked movement.
        for (var tick = 0; tick < 20; tick++)
        {
            state = service.Advance(state, content, routes, new SimulationTick(tick)).State;
            if (!service.GetCreepSnapshots(state, content, routes)[0].Position.Equals(new GridPosition(0, 1)))
            {
                break;
            }
        }

        var snapshot = Assert.Single(service.GetCreepSnapshots(state, content, routes));

        Assert.Equal(new EntityId(1), snapshot.EntityId);
        Assert.Equal(RunnerCreepId, snapshot.CreepId);
        Assert.Equal(new PlayerId(2), snapshot.SenderId);
        Assert.Equal(new GridPosition(1, 1), snapshot.Position);
        Assert.Equal(10, snapshot.Health);
        Assert.Equal(10, snapshot.MaxHealth);
        Assert.Equal(1, snapshot.SpeedPerSecond);
    }

    [Fact]
    public void Pulse_tower_splashes_nearby_creeps()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes();
        var state = new CombatState(
            new[]
            {
                service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne),
                service.SpawnCreep(new EntityId(2), Runner(), new PlayerId(2), LaneOne)
            },
            new[] { new TowerCombatState(new EntityId(10), PulseTowerId, new PlayerId(1), LaneOne, new GridPosition(1, 1)) });

        var result = service.Advance(state, content, routes, new SimulationTick(0));

        Assert.Contains(result.State.Creeps, creep => creep.EntityId.Equals(new EntityId(1)) && creep.Health == 2);
        Assert.Contains(result.State.Creeps, creep => creep.EntityId.Equals(new EntityId(2)) && creep.Health == 6);
        Assert.Equal(2, result.Events.OfType<CreepDamagedEvent>().Count());
        Assert.Single(result.Events.OfType<TowerFiredEvent>());
    }

    [Fact]
    public void Prism_prioritizes_shade_and_bypasses_shade_resistance()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes();
        var state = new CombatState(
            new[]
            {
                service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne),
                service.SpawnCreep(new EntityId(2), Shade(), new PlayerId(2), LaneOne)
            },
            new[] { new TowerCombatState(new EntityId(10), PrismTowerId, new PlayerId(1), LaneOne, new GridPosition(1, 1)) });

        var result = service.Advance(state, content, routes, new SimulationTick(0));

        Assert.Contains(result.Events, simulationEvent => simulationEvent is CreepDamagedEvent damaged && damaged.CreepEntityId.Equals(new EntityId(2)) && damaged.DamageDealt == 12);
        Assert.Contains(result.State.Creeps, creep => creep.EntityId.Equals(new EntityId(1)) && creep.Health == 10);
        Assert.Contains(result.State.Creeps, creep => creep.EntityId.Equals(new EntityId(2)) && creep.Health == 2);
    }

    [Fact]
    public void Shade_resists_non_detection_tower_damage()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes();
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Shade(), new PlayerId(2), LaneOne) },
            new[] { new TowerCombatState(new EntityId(10), ArrowTowerId, new PlayerId(1), LaneOne, new GridPosition(1, 1)) });

        var result = service.Advance(state, content, routes, new SimulationTick(0));

        Assert.Contains(result.Events, simulationEvent => simulationEvent is CreepDamagedEvent damaged && damaged.DamageDealt == 3);
        Assert.Contains(result.State.Creeps, creep => creep.EntityId.Equals(new EntityId(1)) && creep.Health == 11);
    }

    /// <summary>Every creep costs the defender exactly one life, heavies included.</summary>
    /// <remarks>
    /// Was <c>Siege_creep_emits_extra_leak_loss</c>, asserting 2. Flattened to 1:1 on 2026-08-09 by
    /// request. Kept rather than deleted, and inverted rather than weakened: the roster's heaviest
    /// creeps are exactly where a life multiplier would creep back in, so the rule needs a test
    /// naming them.
    /// </remarks>
    [Fact]
    public void Siege_creep_still_costs_exactly_one_life()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes(length: 2);
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Siege(), new PlayerId(2), LaneOne) },
            Array.Empty<TowerCombatState>());

        var result = AdvanceUntilLeak(service, state, content, routes);
        var leak = Assert.Single(result.Events.OfType<LeakEvent>());

        Assert.Equal(1, leak.LivesLost.Amount);
    }

    /// <summary>The highest-health creep in the roster costs one life, same as the cheapest.</summary>
    /// <remarks>
    /// This test previously asserted the opposite, and the reversal is deliberate. The old rule read
    /// "siege" or "colossus" out of the content id — a name heuristic standing in for a content
    /// field, flagged as a design question when written and never resolved. 1:1 retires the
    /// heuristic rather than promoting it.
    ///
    /// creep.colossus is "Siege Colossus" at 90 max health against creep.siege's 48, so it is the
    /// creep most likely to have a multiplier reintroduced on the grounds that it "should" hurt
    /// more. It buys durability and speed with its cost; it does not also buy a life multiplier the
    /// defender cannot see coming.
    /// </remarks>
    [Fact]
    public void Colossus_creep_still_costs_exactly_one_life()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes(length: 2);
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Colossus(), new PlayerId(2), LaneOne) },
            Array.Empty<TowerCombatState>());

        var result = AdvanceUntilLeak(service, state, content, routes);
        var leak = Assert.Single(result.Events.OfType<LeakEvent>());

        Assert.Equal(1, leak.LivesLost.Amount);
    }

    /// <summary>
    /// Advances until a <see cref="LeakEvent"/> is emitted, returning the tick result that carried it.
    /// </summary>
    /// <remarks>
    /// These tests used to call Advance once and assume the creep had crossed a two-cell route,
    /// which was true while a creep covered a whole cell per tick. CombatService.BaseMovementCost
    /// makes a cell cost three ticks, so a single Advance no longer reaches the exit. Waiting for the
    /// leak keeps the assertions about what a leak COSTS, which is what these tests are for, rather
    /// than about how many ticks the walk to the exit takes.
    /// </remarks>
    private static CombatTickResult AdvanceUntilLeak(
        CombatService service,
        CombatState state,
        CombatContent content,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> routes,
        int maxTicks = 40)
    {
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            var result = service.Advance(state, content, routes, new SimulationTick(tick));
            if (result.Events.OfType<LeakEvent>().Any())
            {
                return result;
            }

            state = result.State;
        }

        throw new InvalidOperationException($"no creep leaked within {maxTicks} ticks");
    }

    private static readonly LaneId LaneOne = new(1);

    private static readonly ContentId ArrowTowerId = new("tower.arrow");

    private static readonly ContentId PulseTowerId = new("tower.pulse");

    private static readonly ContentId PrismTowerId = new("tower.prism");

    private static readonly ContentId RunnerCreepId = new("creep.runner");

    private static readonly ContentId ShadeCreepId = new("creep.shade");

    private static readonly ContentId SiegeCreepId = new("creep.siege");

    private static readonly ContentId ColossusCreepId = new("creep.colossus");

    private static CombatContent CreateContent() =>
        new(
            new[] { Runner(), Shade(), Siege(), Colossus() },
            new[] { ArrowTower(), PulseTower(), PrismTower() },
            new Dictionary<LaneId, PlayerId> { [LaneOne] = new PlayerId(1) });

    private static IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> CreateRoutes(int length = 5)
    {
        var route = Enumerable.Range(0, length)
            .Select(x => new GridPosition(x, 1))
            .ToArray();

        return new Dictionary<LaneId, IReadOnlyList<GridPosition>> { [LaneOne] = route };
    }

    private static CreepDefinition Runner() =>
        new(RunnerCreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 10, speedPerSecond: 1, categoryIndex: 0);

    private static CreepDefinition Shade() =>
        new(ShadeCreepId, "Shade", new Gold(24), new Income(3), new Gold(2), new Gold(4), maxHealth: 14, speedPerSecond: 1, categoryIndex: 0);

    private static CreepDefinition Siege() =>
        new(SiegeCreepId, "Siege", new Gold(40), new Income(4), new Gold(4), new Gold(6), maxHealth: 48, speedPerSecond: 1, categoryIndex: 0);

    private static CreepDefinition Colossus() =>
        new(ColossusCreepId, "Siege Colossus", new Gold(52), new Income(5), new Gold(5), new Gold(8), maxHealth: 90, speedPerSecond: 1, categoryIndex: 0);

    private static TowerDefinition ArrowTower() =>
        new(ArrowTowerId, "Arrow Tower", new Gold(25), rangeCells: 2, damage: 5, attackCooldownTicks: 2, categoryIndex: 0);

    private static TowerDefinition PulseTower() =>
        new(PulseTowerId, "Pulse Tower", new Gold(45), rangeCells: 2, damage: 8, attackCooldownTicks: 4, categoryIndex: 0);

    private static TowerDefinition PrismTower() =>
        new(PrismTowerId, "Prism Tower", new Gold(60), rangeCells: 4, damage: 12, attackCooldownTicks: 6, categoryIndex: 0);
}
