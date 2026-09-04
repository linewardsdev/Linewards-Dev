namespace LTW.MatchServer.Wire;

/// <summary>
/// Every message a client sends is one of these, chosen by its own <c>type</c> field — see
/// <see cref="MatchConnection"/> for how an incoming frame is routed to one of these shapes.
/// Deliberately plain DTOs rather than the <c>LTW.Simulation</c> primitive types
/// (<c>PlayerId</c>, <c>LaneId</c>, <c>ContentId</c>...): those are constructor-validated value
/// types with no default constructor <c>System.Text.Json</c> can deserialize into, and the wire
/// format is this project's concern, not the simulation's — <c>MatchConnection.Dispatch</c> is
/// the one place that converts between the two.
///
/// <see cref="Id"/> is optional and, if the client sent one, is echoed back verbatim on the
/// matching <see cref="ServerMessages.CommandResultMessage"/> so a client with more than one
/// in-flight request can tell which reply is which.
/// </summary>
public abstract class ClientCommandMessage
{
    public string? Id { get; set; }
}

public sealed class PlaceTowerMessage : ClientCommandMessage
{
    public int LaneId { get; set; }
    public string TowerId { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class QueueSendMessage : ClientCommandMessage
{
    public string CreepId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public sealed class BuyCategoryTierMessage : ClientCommandMessage
{
    /// <summary>0 = TowerLine, 1 = SendCategory — matches <c>LTW.Simulation.Commands.CategoryKind</c>'s declaration order.</summary>
    public int CategoryKind { get; set; }
    public int CategoryIndex { get; set; }
    public int TargetTier { get; set; }
}

public sealed class UpgradeTowerMessage : ClientCommandMessage
{
    public int LaneId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class SellTowerMessage : ClientCommandMessage
{
    public int LaneId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
