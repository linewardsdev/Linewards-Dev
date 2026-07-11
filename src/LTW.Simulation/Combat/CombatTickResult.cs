using System.Collections.Generic;
using LTW.Simulation.Events;

namespace LTW.Simulation.Combat;

public sealed class CombatTickResult
{
    public CombatTickResult(CombatState state, IReadOnlyList<ISimulationEvent> events)
    {
        State = state;
        Events = events;
    }

    public CombatState State { get; }

    public IReadOnlyList<ISimulationEvent> Events { get; }
}
