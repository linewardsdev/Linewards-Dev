using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Combat;

public sealed class CreepPresentationSnapshot
{
    public CreepPresentationSnapshot(EntityId entityId, ContentId creepId, PlayerId senderId, GridPosition position, int health)
    {
        EntityId = entityId;
        CreepId = creepId;
        SenderId = senderId;
        Position = position;
        Health = health;
    }

    public EntityId EntityId { get; }

    public ContentId CreepId { get; }

    public PlayerId SenderId { get; }

    public GridPosition Position { get; }

    public int Health { get; }
}
