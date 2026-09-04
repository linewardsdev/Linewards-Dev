using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Replay;

/// <summary>
/// A full, reissuable log of one <c>LocalVerticalSlice</c> match: every accepted command, with
/// enough data to reissue it, in the order it was accepted.
/// </summary>
/// <remarks>
/// Deliberately a new, separate type from <see cref="ReplayRecord"/> rather than an extension of
/// it. <see cref="ReplayRecord"/> is shared with <c>ScenarioRunner</c>'s economy-only balance
/// sweeps, which have no towers or lanes to record and whose own remarks say plainly it "cannot
/// reproduce a match" and that full replay "would be a real feature, not a bug fix" — this is that
/// feature, scoped to where it is actually needed (<c>LocalVerticalSlice</c>) rather than bolted
/// onto a type a second, unrelated consumer already depends on. See
/// <c>docs/MULTIPLAYER_ROLLOUT.md</c>'s MP-00 for why now: everything past it (a seat table,
/// recorded opponents, an authoritative server) needs a match that can prove it replayed
/// identically, and nothing before this record could.
///
/// What is still NOT recorded, on purpose, matching the send queue's own documented split between
/// intent and fact: <c>EnqueueSend</c>, <c>CancelQueuedSend</c> and <c>ClearSendQueue</c> touch a
/// player's private queue, not the match; only the <c>Send</c> a queue eventually resolves to
/// (via <c>QueueSend</c>) is ever recorded, exactly once, at the tick gold actually reached it.
///
/// What replaying this record does NOT do: reproduce bot DECISIONS. It reproduces their EFFECTS.
/// Every bot action reaches the match through the same public command methods a human uses (see
/// <c>LocalVerticalSlice.BotMatchContext</c>), so a full command log already captures a bot's
/// moves without needing to re-run its AI — which is also why <c>LocalVerticalSlice.Replay</c>
/// disables bots entirely rather than seeding them to decide the same way twice.
///
/// What this does NOT yet give MULTIPLAYER_SEATS_AND_AUTHORITY.md's command queue: commands are
/// still applied the moment they are accepted, not deferred to a tick boundary and merged with
/// other sources in canonical order. <see cref="RecordedCommand.Sequence"/> exists as the seam
/// that later work slots into — it already is the canonical order, because today there is only
/// ever one call stack to order.
/// </remarks>
public sealed class MatchReplayRecord
{
    public MatchReplayRecord(
        int seed,
        string contentVersion,
        ContentId mapId,
        IReadOnlyList<PlayerId> players,
        SimulationTick completedAtTick,
        IReadOnlyList<RecordedCommand> commands)
    {
        Seed = seed;
        ContentVersion = contentVersion;
        MapId = mapId;
        Players = players.ToArray();
        CompletedAtTick = completedAtTick;
        Commands = commands.ToArray();
    }

    /// <summary>Consumed by <c>LocalVerticalSlice.Replay</c> only to the extent the caller's own
    /// <c>LocalMatchOptions</c> already encodes it — see that method's remarks.</summary>
    public int Seed { get; }

    public string ContentVersion { get; }

    public ContentId MapId { get; }

    public IReadOnlyList<PlayerId> Players { get; }

    public SimulationTick CompletedAtTick { get; }

    public IReadOnlyList<RecordedCommand> Commands { get; }
}
