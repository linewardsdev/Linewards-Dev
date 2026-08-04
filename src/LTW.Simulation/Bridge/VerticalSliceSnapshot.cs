using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Combat;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

/// <remarks>
/// Creeps/Towers/TowerAimTargets are copied at construction (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items) so a caller
/// holding an old snapshot cannot observe values change out from under it — the same defensive-copy
/// convention already used by State/Snapshots.cs and ReplayRecord.
/// </remarks>
public sealed class VerticalSliceSnapshot
{
    public VerticalSliceSnapshot(
        SimulationTick tick,
        EconomyPlayerSet players,
        IReadOnlyList<CreepPresentationSnapshot> creeps,
        IReadOnlyList<TowerCombatState> towers,
        IReadOnlyList<TowerAimSnapshot> towerAimTargets,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> brambleCells)
    {
        Tick = tick;
        Players = players;
        Creeps = creeps.ToArray();
        Towers = towers.ToArray();
        TowerAimTargets = towerAimTargets.ToArray();
        BrambleCells = brambleCells.ToDictionary(lane => lane.Key, lane => (IReadOnlyList<GridPosition>)lane.Value.ToArray());
    }

    public SimulationTick Tick { get; }

    public EconomyPlayerSet Players { get; }

    public IReadOnlyList<CreepPresentationSnapshot> Creeps { get; }

    public IReadOnlyList<TowerCombatState> Towers { get; }

    public IReadOnlyList<TowerAimSnapshot> TowerAimTargets { get; }

    /// <summary>Grid cells under a Thorn Snare's brambles, per lane, so the client can draw the brake.</summary>
    /// <remarks>
    /// Copied at construction like everything else here. Empty for a lane with no thorn tower, and
    /// absent entirely rather than present-and-empty, so a renderer can skip a lane with one lookup.
    /// </remarks>
    public IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> BrambleCells { get; }
}
