using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Combat;
using LTW.Simulation.Economy;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

/// <remarks>
/// Creeps/Towers/TowerAimTargets are copied at construction (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items) so a caller
/// holding an old snapshot cannot observe values change out from under it — the same defensive-copy
/// convention already used by State/Snapshots.cs and ReplayRecord.
/// </remarks>
public sealed class VerticalSliceSnapshot
{
    public VerticalSliceSnapshot(
        SimulationTick tick,
        EconomyPlayerSet players,
        IReadOnlyList<CreepPresentationSnapshot> creeps,
        IReadOnlyList<TowerCombatState> towers,
        IReadOnlyList<TowerAimSnapshot> towerAimTargets,
        IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> brambleCells,
        IReadOnlyDictionary<PlayerId, IReadOnlyList<ContentId>>? sendQueues = null)
    {
        SendQueues = sendQueues ?? new Dictionary<PlayerId, IReadOnlyList<ContentId>>();
        Tick = tick;
        Players = players;
        Creeps = creeps.ToArray();
        Towers = towers.ToArray();
        TowerAimTargets = towerAimTargets.ToArray();
        BrambleCells = brambleCells.ToDictionary(lane => lane.Key, lane => (IReadOnlyList<GridPosition>)lane.Value.ToArray());
    }

    /// <summary>
    /// What each seat has waiting in its send queue, oldest first.
    /// </summary>
    /// <remarks>
    /// Carried in the snapshot rather than read off the simulation, because under a server the
    /// queue is authoritative state that arrives over the wire like gold and lives do. A client
    /// that reached into the simulation for it would be reading a local guess, and the send card's
    /// count would drift the first time a message was dropped or reordered — silently, and only for
    /// the player who queued.
    ///
    /// Optional on the constructor so the many places that build a snapshot for a test or a capture
    /// do not all have to care. Absent means an empty queue, which is the honest answer for a
    /// snapshot nobody attached one to.
    /// </remarks>
    public IReadOnlyDictionary<PlayerId, IReadOnlyList<ContentId>> SendQueues { get; }

    /// <summary>One seat's waiting sends, oldest first.</summary>
    public IReadOnlyList<ContentId> SendQueueFor(PlayerId playerId) =>
        SendQueues.TryGetValue(playerId, out var queue) ? queue : System.Array.Empty<ContentId>();

    /// <summary>How many copies of one creep this seat has waiting.</summary>
    public int QueuedSendCountFor(PlayerId playerId, ContentId creepId)
    {
        var queued = 0;
        foreach (var entry in SendQueueFor(playerId))
        {
            if (entry.Equals(creepId))
            {
                queued++;
            }
        }

        return queued;
    }

    public SimulationTick Tick { get; }

    public EconomyPlayerSet Players { get; }

    public IReadOnlyList<CreepPresentationSnapshot> Creeps { get; }

    public IReadOnlyList<TowerCombatState> Towers { get; }

    public IReadOnlyList<TowerAimSnapshot> TowerAimTargets { get; }

    /// <summary>Grid cells under a Thorn Snare's brambles, per lane, so the client can draw the brake.</summary>
    /// <remarks>
    /// Copied at construction like everything else here. Empty for a lane with no thorn tower, and
    /// absent entirely rather than present-and-empty, so a renderer can skip a lane with one lookup.
    /// </remarks>
    public IReadOnlyDictionary<LaneId, IReadOnlyList<GridPosition>> BrambleCells { get; }
}
