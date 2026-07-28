using LTW.Simulation.Bridge;

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
}
