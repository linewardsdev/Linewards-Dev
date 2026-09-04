using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Replay;

/// <summary>
/// Every kind of command <see cref="RecordedCommand"/> can carry. One entry per public command
/// method on <c>LocalVerticalSlice</c> that mutates match state — not the enqueue-only methods
/// (<c>EnqueueSend</c>, <c>CancelQueuedSend</c>, <c>ClearSendQueue</c>), which stay unrecorded for
/// the reason <see cref="MatchReplayRecord"/>'s remarks give.
/// </summary>
public enum RecordedCommandKind
{
    PlaceTower,
    SellTower,
    UpgradeTower,
    BuyCategoryTier,
    Send,
}

/// <summary>
/// One command actually accepted into a <c>LocalVerticalSlice</c> match, with enough data to
/// reissue it against a fresh instance of the same match and reach the same state.
/// </summary>
/// <remarks>
/// One class with nullable fields rather than a type per kind: every consumer (the recorder in
/// <c>LocalVerticalSlice</c>, the replayer in <c>LocalVerticalSlice.Replay</c>) already switches
/// on <see cref="Kind"/>, and a hierarchy would only move that switch from one place to five
/// constructors. The factory methods below are what keep an instance from ever pairing a kind
/// with the wrong fields — there is no public constructor that could.
/// </remarks>
public sealed class RecordedCommand
{
    private RecordedCommand(
        SimulationTick tick,
        int sequence,
        PlayerId playerId,
        RecordedCommandKind kind,
        LaneId? laneId,
        ContentId? contentId,
        GridPosition? position,
        CategoryKind? category,
        int? categoryIndex,
        int? targetTier,
        int? quantity)
    {
        Tick = tick;
        Sequence = sequence;
        PlayerId = playerId;
        Kind = kind;
        LaneId = laneId;
        ContentId = contentId;
        Position = position;
        Category = category;
        CategoryIndex = categoryIndex;
        TargetTier = targetTier;
        Quantity = quantity;
    }

    public static RecordedCommand ForPlaceTower(SimulationTick tick, int sequence, PlayerId playerId, LaneId laneId, ContentId towerId, GridPosition position) =>
        new(tick, sequence, playerId, RecordedCommandKind.PlaceTower, laneId, towerId, position, null, null, null, null);

    public static RecordedCommand ForSellTower(SimulationTick tick, int sequence, PlayerId playerId, LaneId laneId, GridPosition position) =>
        new(tick, sequence, playerId, RecordedCommandKind.SellTower, laneId, null, position, null, null, null, null);

    public static RecordedCommand ForUpgradeTower(SimulationTick tick, int sequence, PlayerId playerId, LaneId laneId, GridPosition position) =>
        new(tick, sequence, playerId, RecordedCommandKind.UpgradeTower, laneId, null, position, null, null, null, null);

    public static RecordedCommand ForBuyCategoryTier(SimulationTick tick, int sequence, PlayerId playerId, CategoryKind category, int categoryIndex, int targetTier) =>
        new(tick, sequence, playerId, RecordedCommandKind.BuyCategoryTier, null, null, null, category, categoryIndex, targetTier, null);

    public static RecordedCommand ForSend(SimulationTick tick, int sequence, PlayerId playerId, ContentId creepId, int quantity) =>
        new(tick, sequence, playerId, RecordedCommandKind.Send, null, creepId, null, null, null, null, quantity);

    /// <summary>Tick this command was accepted and applied on.</summary>
    public SimulationTick Tick { get; }

    /// <summary>
    /// Monotonic across the whole match, assigned in the order commands were actually accepted —
    /// including two commands sharing a <see cref="Tick"/>, where it is the only record of which
    /// one happened first. That is the ordering key <c>MULTIPLAYER_SEATS_AND_AUTHORITY.md</c>'s
    /// command queue needs once more than one process can submit for the same tick; today, with
    /// every command still applied synchronously from one call stack, it is simply true call order.
    /// </summary>
    public int Sequence { get; }

    public PlayerId PlayerId { get; }

    public RecordedCommandKind Kind { get; }

    public LaneId? LaneId { get; }

    public ContentId? ContentId { get; }

    public GridPosition? Position { get; }

    public CategoryKind? Category { get; }

    public int? CategoryIndex { get; }

    public int? TargetTier { get; }

    public int? Quantity { get; }
}
