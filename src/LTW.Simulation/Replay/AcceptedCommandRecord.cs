using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Replay;

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
