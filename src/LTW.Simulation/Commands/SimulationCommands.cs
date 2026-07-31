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

public sealed class PauseSimulationCommand : ISimulationCommand
{
    public PauseSimulationCommand(PlayerId playerId, SimulationTick requestedTick, bool isPaused)
    {
        PlayerId = playerId;
        RequestedTick = requestedTick;
        IsPaused = isPaused;
    }

    public PlayerId PlayerId { get; }

    public SimulationTick RequestedTick { get; }

    public bool IsPaused { get; }
}
