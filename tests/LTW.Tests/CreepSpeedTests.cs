using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bridge;
using LTW.Simulation.Combat;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

public sealed class CreepSpeedTests
{
    /// <summary>
    /// The Unity renderer interpolates a creep toward its new cell unless it moved further than
    /// UnityVerticalSliceRenderer.CreepTeleportSnapDistance, in which case it snaps, on the
    /// assumption that only a lane transfer moves a creep that far.
    /// </summary>
    /// <remarks>
    /// CombatService.MoveCreeps advances a creep by SpeedPerSecond WHOLE CELLS per tick and one
    /// cell is one world unit, so a creep's speed value IS its per-tick world-space travel. A creep
    /// faster than the snap distance therefore clears that threshold every tick and never
    /// interpolates — it visibly teleports. That is exactly what shipping Crystal Wisp at speed 3
    /// did against the old hardcoded 2.5 threshold.
    ///
    /// This is a cross-boundary coupling the compiler cannot enforce: the speeds live in the
    /// simulation, the threshold lives in the Unity client. The bound is asserted here so raising a
    /// creep's speed past it fails a test instead of silently reintroducing the teleport.
    /// </remarks>
    private const int RendererSnapDistanceInCells = 4; // LaneSpacing (9) * 0.5, floored

    [Fact]
    public void No_creep_moves_far_enough_per_tick_to_trip_the_renderer_snap()
    {
        var content = SampleVerticalSliceContent.Create();

        Assert.NotEmpty(content.Creeps);
        foreach (var creep in content.Creeps)
        {
            Assert.True(
                creep.SpeedPerSecond < RendererSnapDistanceInCells,
                $"{creep.Id.Value} moves {creep.SpeedPerSecond} cells/tick, at or beyond the renderer's " +
                $"{RendererSnapDistanceInCells}-cell snap distance, so it will teleport instead of " +
                "interpolating. Raise CreepTeleportSnapDistance in UnityVerticalSliceRenderer (keeping " +
                "it below LaneSpacing) or lower this creep's speed.");
        }
    }

    [Fact]
    public void Creep_speeds_are_positive()
    {
        var content = SampleVerticalSliceContent.Create();

        foreach (var creep in content.Creeps)
        {
            Assert.True(creep.SpeedPerSecond > 0, $"{creep.Id.Value} has a non-positive speed and would never advance.");
        }
    }

    /// <summary>
    /// Every creep's presentation snapshot reports the same speed and max health as its definition,
    /// for all fifteen — not just the handful a scenario happens to spawn.
    /// </summary>
    /// <remarks>
    /// This is the test that was missing when the client guessed both of these from the creep id
    /// string (OPEN_ITEMS.md item 12). That guess was correct for the original five and wrong for
    /// ten of the fifteen added since, and nothing failed, because no test ever compared the two
    /// sides across the whole roster. Both fields now come from the definition, so this asserts the
    /// property that makes guessing unnecessary rather than re-checking specific numbers that the
    /// balance record is free to change.
    ///
    /// Speed matters to presentation for the same reason max health does: the renderer scales a
    /// rigged creep's walk-clip playback by it, so a wrong value shows up as foot skate rather than
    /// as a wrong health bar.
    /// </remarks>
    [Fact]
    public void Presentation_snapshots_report_each_creeps_own_speed_and_max_health()
    {
        // Built straight against CombatService rather than through LocalVerticalSlice: sending all
        // fifteen costs 369 gold, well past any starting balance, so a match-level version of this
        // test would only ever cover the creeps the economy happened to afford — which is the exact
        // gap that let item 12 through. This spawns one of every creep directly, so the assertion is
        // over the whole roster by construction.
        var content = SampleVerticalSliceContent.Create();
        var service = new CombatService();
        var lane = new LaneId(1);
        var combatContent = new CombatContent(
            content.Creeps,
            content.Towers,
            new Dictionary<LaneId, PlayerId> { [lane] = new PlayerId(2) });
        var routes = new Dictionary<LaneId, IReadOnlyList<GridPosition>>
        {
            [lane] = Enumerable.Range(0, 16).Select(y => new GridPosition(3, y)).ToArray()
        };

        var entityId = 1;
        var state = new CombatState(
            content.Creeps.Select(creep => service.SpawnCreep(new EntityId(entityId++), creep, new PlayerId(1), lane)),
            Array.Empty<TowerCombatState>());

        var snapshots = service.GetCreepSnapshots(state, combatContent, routes);

        Assert.Equal(content.Creeps.Count, snapshots.Count);
        foreach (var creep in content.Creeps)
        {
            var snapshot = Assert.Single(snapshots, candidate => candidate.CreepId.Equals(creep.Id));
            Assert.Equal(creep.SpeedPerSecond, snapshot.SpeedPerSecond);
            Assert.Equal(creep.MaxHealth, snapshot.MaxHealth);
        }
    }
}
