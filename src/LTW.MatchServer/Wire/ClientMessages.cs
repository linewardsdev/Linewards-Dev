namespace LTW.MatchServer.Wire;

/// <summary>
/// Every message a client sends is one of these, chosen by its own <c>type</c> field — see
/// <see cref="ServerMatch.DispatchAsync"/> for how an incoming frame is routed to one of these
/// shapes (there is no separate <c>MatchConnection</c> class; this comment previously named one
/// that was never actually added — see docs/SECURITY_AUDIT_2026-09-05.md's L5).
/// Deliberately plain DTOs rather than the <c>LTW.Simulation</c> primitive types
/// (<c>PlayerId</c>, <c>LaneId</c>, <c>ContentId</c>...): those are constructor-validated value
/// types with no default constructor <c>System.Text.Json</c> can deserialize into, and the wire
/// format is this project's concern, not the simulation's — <see cref="ServerMatch.DispatchAsync"/>
/// is the one place that converts between the two.
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

/// <summary>
/// Wire equivalent of <c>LocalVerticalSlice.QueueSend</c> — an immediate, all-or-nothing spend and
/// spawn, charged and rejected right now if the sender cannot afford it. NOT what the real send
/// dock UI uses; see <see cref="EnqueueSendMessage"/> for that.
/// </summary>
public sealed class QueueSendMessage : ClientCommandMessage
{
    public string CreepId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Wire equivalent of <c>LocalVerticalSlice.EnqueueSend</c> — adds one creep to the sender's send
/// queue, which the simulation drains as gold becomes available, rather than requiring the full
/// cost up front. This is what a real player's tap of a send-dock card means locally
/// (<c>UnityCommandAdapter.SendCreep</c>'s own remarks: "Queued, not sent... a tap states intent
/// and the simulation pays for it when it can"). Found missing live, testing a real online match:
/// the wire layer only ever had <see cref="QueueSendMessage"/>, so every online send silently
/// failed outright the instant the sender could not afford it immediately, instead of waiting —
/// confusingly, "QueueSend" is the immediate send; this is the actual queue.
/// </summary>
public sealed class EnqueueSendMessage : ClientCommandMessage
{
    public string CreepId { get; set; } = "";
}

/// <summary>
/// Wire equivalent of <c>LocalVerticalSlice.CancelQueuedSend</c> — withdraws the sender's own MOST
/// RECENT queued send of this creep (see that method's own remarks for why the other end of the
/// queue would be the wrong one). Found missing 2026-09-07 alongside <see cref="ClearSendQueueMessage"/>,
/// the same day a real UI control finally reached both on the client's local-play path
/// (OPEN_ITEMS.md item 47) and immediately exposed that neither had ever been given a wire message
/// — every other command on <c>UnityCommandAdapter</c> already checks for a <c>wireClient</c> and
/// sends one; these two silently did nothing online instead (OPEN_ITEMS.md item 55).
/// </summary>
public sealed class CancelSendMessage : ClientCommandMessage
{
    public string CreepId { get; set; } = "";
}

/// <summary>
/// Wire equivalent of <c>LocalVerticalSlice.ClearSendQueue</c> — empties the sender's whole send
/// queue in one call. See <see cref="CancelSendMessage"/>'s own remarks for why this exists now.
/// </summary>
public sealed class ClearSendQueueMessage : ClientCommandMessage
{
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
