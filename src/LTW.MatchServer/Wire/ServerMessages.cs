namespace LTW.MatchServer.Wire;

/// <summary>Every message this server ever sends carries a <c>type</c> discriminator matching the class name below.</summary>
public static class ServerMessageType
{
    public const string Welcome = "welcome";
    public const string CommandResult = "commandResult";
    public const string Tick = "tick";
    public const string Error = "error";
}

/// <summary>Sent once, immediately after a connection's join token is accepted.</summary>
public sealed class WelcomeMessage
{
    public string Type { get; } = ServerMessageType.Welcome;
    public int Seat { get; set; }
    public long Tick { get; set; }
}

/// <summary>
/// Answers one <see cref="ClientCommandMessage"/> — <see cref="Id"/> echoes the client's own,
/// if it sent one, so a client with several in-flight requests can match replies to requests.
/// </summary>
public sealed class CommandResultMessage
{
    public string Type { get; } = ServerMessageType.CommandResult;
    public string? Id { get; set; }
    public bool Accepted { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// One accepted <c>ISimulationEvent</c> that happened during a tick. <see cref="Data"/> is the
/// event's own concrete type serialized generically (<c>JsonSerializer.Serialize(evt,
/// evt.GetType())</c>) rather than a hand-written DTO per event class — MP-00's own note on the
/// command log applies here too: there are a few dozen event types, and a hand-maintained mirror
/// of each is a second copy of a shape that already exists and already changes when gameplay does.
/// </summary>
public sealed class EventDto
{
    public string Kind { get; set; } = "";
    public object? Data { get; set; }
}

/// <summary>
/// Everything one tick produced, in ONE message rather than one send per event plus a separate
/// snapshot send. That was the first version of this protocol, and it measurably could not keep
/// up with a fast server loop in practice — see docs/MULTIPLAYER_ROLLOUT.md's MP-04 notes on the
/// throughput this replaced (roughly 33 delivered ticks/second against a requested 200, entirely
/// per-message overhead) versus what this version measures.
/// </summary>
/// <remarks>
/// <see cref="Creeps"/> was added for MP-06 — MP-04 deliberately scoped it out ("client rendering
/// fidelity is Unity's concern, not this initiative's"), which was correct for proving the
/// transport, but a client cannot render an actual match without it. It mirrors
/// <c>LTW.Simulation.Combat.CreepPresentationSnapshot</c>, the same shape the LOCAL renderer
/// already uses, field for field — a wire-based renderer should not need a second interpretation
/// of movement interpolation from the one Practice already has.
/// </remarks>
public sealed class TickMessage
{
    public string Type { get; } = ServerMessageType.Tick;
    public long Tick { get; set; }

    /// <summary>Increments every loop iteration, ticking or not — unlike <see cref="Tick"/>, which
    /// is frozen at 0 for the whole opening build window (see <see cref="IsOpeningBuildCountdown"/>),
    /// this is what a client should key "is this a new message" off of.</summary>
    public long Sequence { get; set; }

    /// <summary>True while <c>ServerMatch</c>'s opening build window is still running — the
    /// simulation has not advanced past tick 0 and no creeps have been sent yet, but placement
    /// commands still work normally.</summary>
    public bool IsOpeningBuildCountdown { get; set; }

    public double OpeningBuildCountdownRemainingSeconds { get; set; }

    public List<EventDto> Events { get; set; } = new();
    public List<PlayerSnapshotDto> Players { get; set; } = new();
    public List<TowerSnapshotDto> Towers { get; set; } = new();
    public List<CreepSnapshotDto> Creeps { get; set; } = new();
}

/// <summary>
/// <see cref="PlayerSnapshotDto.ChosenTowerLine"/>/<see cref="PlayerSnapshotDto.TowerLineTiers"/>/
/// <see cref="PlayerSnapshotDto.SendCategoryTiers"/> were missing from the original MP-06 pass —
/// found in a self-audit immediately after, not by a live test. Their absence meant a
/// wire-reconstructed <c>PlayerEconomyState</c> always read as tier-1/uncommitted regardless of
/// real purchases, which silently broke every tier purchase past the first:
/// <c>UnityCommandAdapter.BuyCategoryTier</c>'s wire-sent <c>TargetTier</c> is <c>current + 1</c>,
/// so a "current" that always reads as 1 sends <c>TargetTier: 2</c> on every purchase, not just
/// the first.
/// </summary>
public sealed class PlayerSnapshotDto
{
    public int PlayerId { get; set; }
    public int Gold { get; set; }
    public int Income { get; set; }
    public int Lives { get; set; }
    public bool Eliminated { get; set; }

    /// <summary>-1 (PlayerEconomyState.UnchosenTowerLine) if not yet committed.</summary>
    public int ChosenTowerLine { get; set; }

    /// <summary>Always PlayerEconomyState.CategoryCount (3) entries, index-aligned with the game's tower lines.</summary>
    public int[] TowerLineTiers { get; set; } = System.Array.Empty<int>();

    /// <summary>Always PlayerEconomyState.CategoryCount (3) entries, index-aligned with the game's send categories.</summary>
    public int[] SendCategoryTiers { get; set; } = System.Array.Empty<int>();

    /// <summary>
    /// This seat's send queue, oldest first — ContentId values as strings. Was missing entirely
    /// until a live test found it: <c>VerticalSliceSnapshot.SendQueues</c>'s own doc comment
    /// already claimed the queue "arrives over the wire like gold and lives do", but nothing had
    /// ever actually put it on this DTO, so a wire client's queue badge always read zero even
    /// after a send was genuinely queued and later spawned.
    /// </summary>
    public string[] SendQueue { get; set; } = System.Array.Empty<string>();
}

public sealed class TowerSnapshotDto
{
    public long EntityId { get; set; }
    public string TowerId { get; set; } = "";
    public int OwnerId { get; set; }
    public int LaneId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Tier { get; set; }
}

/// <summary>
/// Field-for-field mirror of <c>LTW.Simulation.Combat.CreepPresentationSnapshot</c> — see that
/// class's own remarks for why each field exists (in particular <see cref="MovementProgress"/>/
/// <see cref="EffectiveMovementCost"/>, which is what lets a renderer place a creep BETWEEN cells
/// rather than snapping it once per tick).
/// </summary>
public sealed class CreepSnapshotDto
{
    public long EntityId { get; set; }
    public string CreepId { get; set; } = "";
    public int SenderId { get; set; }
    public int LaneId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int SpeedPerSecond { get; set; }
    public int NextX { get; set; }
    public int NextY { get; set; }
    public int MovementProgress { get; set; }
    public int EffectiveMovementCost { get; set; }
    public bool IsBraked { get; set; }
}

/// <summary>A protocol-level problem (bad JSON, unknown message type, not yet authenticated) — not a rejected command, which gets <see cref="CommandResultMessage"/> instead.</summary>
public sealed class ErrorMessage
{
    public string Type { get; } = ServerMessageType.Error;
    public string Message { get; set; } = "";
}
