using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Replay;

/// <summary>
/// One accepted send command: who, what, when, how many. Only <c>QueueSend</c> produces these —
/// <c>PlaceTower</c> and <c>SellTower</c> are not recorded, and there is no field here (lane,
/// position) that could hold a placement even if they were. See <see cref="ReplayRecord"/>'s
/// remarks for why that is deliberate scope, not a gap to fill.
/// </summary>
public sealed class AcceptedCommandRecord
{
    public AcceptedCommandRecord(SimulationTick tick, PlayerId playerId, ContentId contentId, int quantity)
    {
        Tick = tick;
        PlayerId = playerId;
        ContentId = contentId;
        Quantity = quantity;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public ContentId ContentId { get; }

    public int Quantity { get; }
}
