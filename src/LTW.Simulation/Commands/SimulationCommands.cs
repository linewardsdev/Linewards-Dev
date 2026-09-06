using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Commands;

public interface ISimulationCommand
{
    PlayerId PlayerId { get; }

    SimulationTick RequestedTick { get; }
}

public sealed class PlaceTowerCommand : ISimulationCommand
{
    public PlaceTowerCommand(PlayerId playerId, SimulationTick requestedTick, LaneId laneId, ContentId towerId, GridPosition position)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        LaneId = laneId;
        TowerId = towerId;
        Position = position;
    }

    public PlayerId PlayerId { get; }

    public SimulationTick RequestedTick { get; }

    public LaneId LaneId { get; }

    public ContentId TowerId { get; }

    public GridPosition Position { get; }
}

public sealed class SellTowerCommand : ISimulationCommand
{
    public SellTowerCommand(PlayerId playerId, SimulationTick requestedTick, EntityId towerEntityId)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        TowerEntityId = towerEntityId;
    }

    public PlayerId PlayerId { get; }

    public SimulationTick RequestedTick { get; }

    public EntityId TowerEntityId { get; }
}

public sealed class QueueSendCommand : ISimulationCommand
{
    public QueueSendCommand(PlayerId playerId, SimulationTick requestedTick, ContentId creepId, int quantity)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        CreepId = creepId;
        Quantity = quantity;
    }

    public PlayerId PlayerId { get; }

    public SimulationTick RequestedTick { get; }

    public ContentId CreepId { get; }

    public int Quantity { get; }
}

/// <summary>
/// Asks for one creep to be added to this seat's send queue, to be paid for when it can be.
/// </summary>
/// <remarks>
/// A command rather than a method call on the bridge, because a queued send has to survive the
/// trip to an authoritative server. `ARCHITECTURE.md` puts rate limits and cooldowns server-side,
/// and anything a client can ask for has to arrive as something the server validates, accepts or
/// refuses, and can throttle — a bridge method reachable only in-process cannot be any of those.
///
/// Carries one creep, not a quantity. A batch would let a single message enqueue ten entries and
/// make the per-creep cap a function of message count rather than of queue depth; one command per
/// entry keeps the cap meaning the same thing however the client batches its taps.
///
/// Deliberately NOT recorded in the accepted-command stream, and that is worth stating because it
/// looks like an omission. The queue is per-seat private intent; what the match is made of is the
/// SEND that results, which <c>QueueSend</c> already records at the tick gold actually reached it.
/// Recording both would replay the intent and its effect and double every queued send.
/// </remarks>
public sealed class EnqueueSendCommand : ISimulationCommand
{
    public EnqueueSendCommand(PlayerId playerId, SimulationTick requestedTick, ContentId creepId)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        CreepId = creepId;
    }

    public PlayerId PlayerId { get; }

    public SimulationTick RequestedTick { get; }

    public ContentId CreepId { get; }
}

/// <summary>
/// Removes one queued send that has not yet been paid for and dispatched.
/// </summary>
/// <remarks>
/// A command rather than a direct list edit for the same reason EnqueueSendCommand is one: the day
/// this runs against an authoritative server, a cancel has to travel, be validated and be accepted
/// or refused on the same terms as the enqueue it undoes. A client that could only add would leave
/// players unable to take back a mis-tap, which on a touch screen is the more common mistake.
/// </remarks>
public sealed class CancelQueuedSendCommand : ISimulationCommand
{
    public CancelQueuedSendCommand(PlayerId playerId, SimulationTick requestedTick, ContentId creepId)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        CreepId = creepId;
    }

    public PlayerId PlayerId { get; }

    public SimulationTick RequestedTick { get; }

    public ContentId CreepId { get; }
}


/// <summary>
/// Which side of the roster a category tier applies to.
/// </summary>
public enum CategoryKind
{
    TowerLine = 0,
    SendCategory = 1
}

/// <summary>
/// Buys the next tier for one category, raising that category's tower damage or creep health.
/// </summary>
/// <remarks>
/// Deliberately NOT modelled as a <see cref="BuyTechCommand"/>. TechDefinition describes
/// UNLOCKING content it names by id (UnlocksTowerIds/UnlocksCreepIds); this levels content the
/// player already has. Reusing it would have left a type called "tech" doing neither job clearly.
///
/// <c>TargetTier</c> is stated rather than implied ("buy the next one") so the command is
/// self-describing in a replay: reading the accepted-command stream tells you which tier was
/// bought without also having to reconstruct what the player's tier was at that moment.
/// </remarks>
public sealed class BuyCategoryTierCommand : ISimulationCommand
{
    public BuyCategoryTierCommand(PlayerId playerId, SimulationTick requestedTick, CategoryKind categoryKind, int categoryIndex, int targetTier)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        CategoryKind = categoryKind;
        CategoryIndex = categoryIndex;
        TargetTier = targetTier;
    }

    public PlayerId PlayerId { get; }
    public SimulationTick RequestedTick { get; }
    public CategoryKind CategoryKind { get; }
    public int CategoryIndex { get; }
    public int TargetTier { get; }
}

public sealed class BuyTechCommand : ISimulationCommand
{
    public BuyTechCommand(PlayerId playerId, SimulationTick requestedTick, ContentId techId)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        TechId = techId;
    }

    public PlayerId PlayerId { get; }

    public SimulationTick RequestedTick { get; }

    public ContentId TechId { get; }
}
