using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CreepPresentationSnapshot
{
    public CreepPresentationSnapshot(EntityId entityId, ContentId creepId, PlayerId senderId, LaneId laneId, GridPosition position, int health, int maxHealth, int speedPerSecond, GridPosition nextPosition, int movementProgress, int movementCost)
    {
        EntityId = entityId;
        CreepId = creepId;
        SenderId = senderId;
        LaneId = laneId;
        Position = position;
        Health = health;
        MaxHealth = maxHealth;
        SpeedPerSecond = speedPerSecond;
        NextPosition = nextPosition;
        MovementProgress = movementProgress;
        MovementCost = movementCost;
    }

    public EntityId EntityId { get; }

    public ContentId CreepId { get; }

    public PlayerId SenderId { get; }

    public LaneId LaneId { get; }

    public GridPosition Position { get; }

    public int Health { get; }

    public int MaxHealth { get; }

    /// <summary>
    /// The creep definition's speed, in route cells per tick. Carried for presentation so the client
    /// can match a rigged creep's walk-clip playback rate to how fast it actually crosses the board
    /// instead of playing every clip at its authored rate.
    /// </summary>
    /// <remarks>
    /// This is the DEFINITION speed, not the effective one: Thorn Snare's Bramble Hold halves a
    /// creep's speed while it is inside the zone (CombatService.StepCreep), and that is not reflected
    /// here. Exposing the braked value would mean rebuilding bramble zones inside GetCreepSnapshots,
    /// which runs once per rendered FRAME rather than once per tick — the same mistake OPEN_ITEMS.md
    /// item 24 already records against LeadPathIndex. Doing it properly means carrying the braked flag
    /// on CreepCombatState, where MoveCreeps already computes it once per tick; tracked as its own
    /// checklist item rather than paid for at 60 Hz here.
    /// </remarks>
    public int SpeedPerSecond { get; }

    /// <summary>
    /// The route cell this creep is walking toward, or its current cell at the end of the route.
    /// </summary>
    public GridPosition NextPosition { get; }

    /// <summary>
    /// Movement banked toward the next cell, out of <see cref="MovementCost"/>.
    /// </summary>
    /// <remarks>
    /// Exposed with MovementCost rather than as a ready-made fraction because the simulation is
    /// deliberately all-integer — ArchitectureBoundaryTests guards that there is no float or double
    /// arithmetic anywhere in LTW.Simulation, and a 0..1 fraction would be the first. The client
    /// divides these two, which is presentation work and belongs on that side of the boundary.
    ///
    /// Together with NextPosition this is what lets the renderer place a creep BETWEEN cells.
    /// Without it a creep holds a cell for MovementCost ticks and then jumps a whole cell, which at
    /// a base cost of 3 is one hop every 0.75s — the pacing change that made creeps read as too fast
    /// would otherwise have simply traded speed for stutter.
    /// </remarks>
    public int MovementProgress { get; }

    /// <summary>
    /// Movement needed to advance one cell. The unbraked cost: a creep inside a Thorn Snare's
    /// brambles pays double, and that is deliberately NOT reflected here, because knowing it would
    /// mean rebuilding bramble zones inside GetCreepSnapshots, which runs once per rendered FRAME
    /// rather than once per tick. The visible consequence is that a braked creep glides to the next
    /// cell and then holds, which reads as being caught by the brambles rather than as an artefact.
    /// </remarks>
    public int MovementCost { get; }
}
