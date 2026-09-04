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
/// <see cref="Players"/>/<see cref="Towers"/> are NOT creeps or combat state — see MP-04's notes
/// for why that is a deliberately scoped gap (client rendering fidelity is Unity's concern, not
/// this initiative's) rather than an oversight.
/// </remarks>
public sealed class TickMessage
{
    public string Type { get; } = ServerMessageType.Tick;
    public long Tick { get; set; }
    public List<EventDto> Events { get; set; } = new();
    public List<PlayerSnapshotDto> Players { get; set; } = new();
    public List<TowerSnapshotDto> Towers { get; set; } = new();
}

public sealed class PlayerSnapshotDto
{
    public int PlayerId { get; set; }
    public int Gold { get; set; }
    public int Income { get; set; }
    public int Lives { get; set; }
    public bool Eliminated { get; set; }
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

/// <summary>A protocol-level problem (bad JSON, unknown message type, not yet authenticated) — not a rejected command, which gets <see cref="CommandResultMessage"/> instead.</summary>
public sealed class ErrorMessage
{
    public string Type { get; } = ServerMessageType.Error;
    public string Message { get; set; } = "";
}
