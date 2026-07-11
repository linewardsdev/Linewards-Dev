using System.Collections.Generic;
using LTW.Simulation.Combat;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

public sealed class VerticalSliceSnapshot
{
    public VerticalSliceSnapshot(
        SimulationTick tick,
        EconomyPlayerSet players,
        IReadOnlyList<CreepPresentationSnapshot> creeps,
        IReadOnlyList<TowerCombatState> towers)
    {
        Tick = tick;
        Players = players;
        Creeps = creeps;
        Towers = towers;
    }

    public SimulationTick Tick { get; }

    public EconomyPlayerSet Players { get; }

    public IReadOnlyList<CreepPresentationSnapshot> Creeps { get; }

    public IReadOnlyList<TowerCombatState> Towers { get; }
}
