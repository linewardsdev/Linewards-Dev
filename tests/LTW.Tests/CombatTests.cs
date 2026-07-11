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
    public void Creep_reaching_exit_emits_one_leak_event_only()
    {
        var service = new CombatService();
        var content = CreateContent();
        var routes = CreateRoutes(length: 2);
        var state = new CombatState(
            new[] { service.SpawnCreep(new EntityId(1), Runner(), new PlayerId(2), LaneOne) },
            Array.Empty<TowerCombatState>());

        var first = service.Advance(state, content, routes, new SimulationTick(1));
        var second = service.Advance(first.State, content, routes, new SimulationTick(2));

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

            var snapshot = service.GetCreepSnapshots(state, routes).FirstOrDefault();
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

        state = service.Advance(state, content, routes, new SimulationTick(0)).State;
        var snapshot = Assert.Single(service.GetCreepSnapshots(state, routes));

        Assert.Equal(new EntityId(1), snapshot.EntityId);
        Assert.Equal(RunnerCreepId, snapshot.CreepId);
        Assert.Equal(new PlayerId(2), snapshot.SenderId);
        Assert.Equal(new GridPosition(1, 1), snapshot.Position);
        Assert.Equal(10, snapshot.Health);
    }

    private static readonly LaneId LaneOne = new(1);

    private static readonly ContentId ArrowTowerId = new("tower.arrow");

    private static readonly ContentId RunnerCreepId = new("creep.runner");

    private static CombatContent CreateContent() =>
        new(
            new[] { Runner() },
            new[] { ArrowTower() },
            new Dictionary<LaneId, PlayerId> { [LaneOne] = new PlayerId(1) });

    private static IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> CreateRoutes(int length = 5)
    {
        var route = Enumerable.Range(0, length)
            .Select(x => new GridPosition(x, 1))
            .ToArray();

        return new Dictionary<LaneId, IReadOnlyList<GridPosition>> { [LaneOne] = route };
    }

    private static CreepDefinition Runner() =>
        new(RunnerCreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 10, speedPerSecond: 1);

    private static TowerDefinition ArrowTower() =>
        new(ArrowTowerId, "Arrow Tower", new Gold(25), rangeCells: 2, damage: 5, attackCooldownTicks: 2);
}
