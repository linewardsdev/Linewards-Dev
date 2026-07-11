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
