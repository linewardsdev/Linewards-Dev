using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CreepPresentationSnapshot
{
    public CreepPresentationSnapshot(EntityId entityId, ContentId creepId, PlayerId senderId, LaneId laneId, GridPosition position, int health)
    {
        EntityId = entityId;
        CreepId = creepId;
        SenderId = senderId;
        LaneId = laneId;
        Position = position;
        Health = health;
    }

    public EntityId EntityId { get; }

    public ContentId CreepId { get; }

    public PlayerId SenderId { get; }

    public LaneId LaneId { get; }

    public GridPosition Position { get; }

    public int Health { get; }
}
