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
        IReadOnlyList<TowerAimSnapshot> towerAimTargets)
    {
        Tick = tick;
        Players = players;
        Creeps = creeps.ToArray();
        Towers = towers.ToArray();
        TowerAimTargets = towerAimTargets.ToArray();
    }

    public SimulationTick Tick { get; }

    public EconomyPlayerSet Players { get; }

    public IReadOnlyList<CreepPresentationSnapshot> Creeps { get; }

    public IReadOnlyList<TowerCombatState> Towers { get; }

    public IReadOnlyList<TowerAimSnapshot> TowerAimTargets { get; }
}
