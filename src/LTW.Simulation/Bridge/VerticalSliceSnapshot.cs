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
        IReadOnlyDictionary<PlayerId, IReadOnlyList<ContentId>>? sendQueues = null,
        LTW.Simulation.Seats.SeatTable? seatTable = null)
    {
        SendQueues = sendQueues ?? new Dictionary<PlayerId, IReadOnlyList<ContentId>>();
        SeatTable = seatTable ?? new LTW.Simulation.Seats.SeatTable(System.Array.Empty<LTW.Simulation.Seats.Seat>());
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

    /// <summary>
    /// Every seat, so a client can show who is a human, a bot, or a placeholder — see
    /// <see cref="LTW.Simulation.Seats.SeatTable"/>. Empty rather than null for a snapshot nobody attached one to,
    /// same convention as <see cref="SendQueues"/> above.
    /// </summary>
    public LTW.Simulation.Seats.SeatTable SeatTable { get; }

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

    /// <summary>
    /// A short, order-independent fingerprint of everything on this snapshot that a replayed
    /// match must reproduce exactly: every player's economy and tiers, every tower, every creep.
    /// Two matches that reach the same tick with the same fingerprint reached the same state —
    /// this is the check <c>LocalVerticalSlice.Replay</c>'s own remarks promise, and the primitive
    /// a future authoritative server needs to prove its replay of a match matches what shipped
    /// (MULTIPLAYER_SEATS_AND_AUTHORITY.md's MP-04).
    /// </summary>
    /// <remarks>
    /// Sorted by entity id before hashing rather than trusting list order: <see cref="Towers"/> and
    /// <see cref="Creeps"/> are built by concatenation and filtering, not by any documented order,
    /// so two runs that reached an identical BOARD could still differ in list order alone. Send
    /// queues are deliberately excluded — they are per-seat, not-yet-committed intent, and this
    /// exists to check what the match actually DID, which is exactly what the queue has not done
    /// yet.
    /// </remarks>
    public string Fingerprint()
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("tick=").Append(Tick.Value);

        foreach (var player in Players.Players.OrderBy(player => player.PlayerId.Value))
        {
            builder.Append("|p=").Append(player.PlayerId.Value)
                .Append(':').Append(player.Gold.Amount)
                .Append(':').Append(player.Income.Amount)
                .Append(':').Append(player.Lives.Amount)
                .Append(':').Append(player.IsEliminated)
                .Append(':').Append(player.NextSendAvailableTick.Value)
                .Append(':').Append(player.ChosenTowerLine);
            for (var category = 0; category < Economy.PlayerEconomyState.CategoryCount; category++)
            {
                builder.Append(':').Append(player.TowerLineTier(category)).Append(',').Append(player.SendCategoryTier(category));
            }
        }

        foreach (var tower in Towers.OrderBy(tower => tower.EntityId.Value))
        {
            builder.Append("|t=").Append(tower.EntityId.Value)
                .Append(':').Append(tower.TowerId.Value)
                .Append(':').Append(tower.OwnerId.Value)
                .Append(':').Append(tower.LaneId.Value)
                .Append(':').Append(tower.Position.X).Append(',').Append(tower.Position.Y)
                .Append(':').Append(tower.Tier);
        }

        foreach (var creep in Creeps.OrderBy(creep => creep.EntityId.Value))
        {
            builder.Append("|c=").Append(creep.EntityId.Value)
                .Append(':').Append(creep.CreepId.Value)
                .Append(':').Append(creep.SenderId.Value)
                .Append(':').Append(creep.LaneId.Value)
                .Append(':').Append(creep.Position.X).Append(',').Append(creep.Position.Y)
                .Append(':').Append(creep.Health)
                .Append(':').Append(creep.MovementProgress);
        }

        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(builder.ToString()));
        var hex = new System.Text.StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            hex.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }
}
