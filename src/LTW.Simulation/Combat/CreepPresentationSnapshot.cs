using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CreepPresentationSnapshot
{
    public CreepPresentationSnapshot(EntityId entityId, ContentId creepId, PlayerId senderId, LaneId laneId, GridPosition position, int health, int maxHealth, int speedPerSecond)
    {
        EntityId = entityId;
        CreepId = creepId;
        SenderId = senderId;
        LaneId = laneId;
        Position = position;
        Health = health;
        MaxHealth = maxHealth;
        SpeedPerSecond = speedPerSecond;
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
}
