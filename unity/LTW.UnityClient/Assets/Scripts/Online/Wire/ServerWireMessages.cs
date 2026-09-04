#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace LTW.UnityClient.Online.Wire
{
    /// <summary>
    /// Client-side mirror of LTW.MatchServer/Wire/ServerMessages.cs. Field names use explicit
    /// [JsonProperty] rather than a global camelCase naming strategy, so this stays correct even if
    /// someone changes the project's default Newtonsoft settings later for an unrelated reason.
    /// </summary>
    public static class ServerMessageType
    {
        public const string Welcome = "welcome";
        public const string CommandResult = "commandResult";
        public const string Tick = "tick";
        public const string Error = "error";
    }

    public sealed class WelcomeMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("seat")]
        public int Seat { get; set; }

        [JsonProperty("tick")]
        public long Tick { get; set; }
    }

    public sealed class CommandResultMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("accepted")]
        public bool Accepted { get; set; }

        [JsonProperty("rejectionReason")]
        public string? RejectionReason { get; set; }
    }

    public sealed class EventDto
    {
        [JsonProperty("kind")]
        public string Kind { get; set; } = "";

        // Not deserialized into a concrete LTW.Simulation.Events type — see MULTIPLAYER_ROLLOUT.md's
        // MP-06 "Landed" for why the event stream (VFX/audio cues) is a deliberately scoped-out gap
        // in this pass: the wire has no fixed per-event-kind DTO catalog, only the sender's own
        // concrete type serialized generically, so a client would need a mirror of every
        // LTW.Simulation.Events.* shape to consume this losslessly.
        [JsonProperty("data")]
        public object? Data { get; set; }
    }

    public sealed class TickMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("tick")]
        public long Tick { get; set; }

        /// <summary>Increments every server loop iteration, ticking or not — unlike <see cref="Tick"/>,
        /// which is frozen at 0 for the whole opening build window, this is what should decide
        /// whether a tick message is new.</summary>
        [JsonProperty("sequence")]
        public long Sequence { get; set; }

        [JsonProperty("isOpeningBuildCountdown")]
        public bool IsOpeningBuildCountdown { get; set; }

        [JsonProperty("openingBuildCountdownRemainingSeconds")]
        public double OpeningBuildCountdownRemainingSeconds { get; set; }

        [JsonProperty("events")]
        public List<EventDto> Events { get; set; } = new();

        [JsonProperty("players")]
        public List<PlayerSnapshotDto> Players { get; set; } = new();

        [JsonProperty("towers")]
        public List<TowerSnapshotDto> Towers { get; set; } = new();

        [JsonProperty("creeps")]
        public List<CreepSnapshotDto> Creeps { get; set; } = new();
    }

    public sealed class PlayerSnapshotDto
    {
        [JsonProperty("playerId")]
        public int PlayerId { get; set; }

        [JsonProperty("gold")]
        public int Gold { get; set; }

        [JsonProperty("income")]
        public int Income { get; set; }

        [JsonProperty("lives")]
        public int Lives { get; set; }

        [JsonProperty("eliminated")]
        public bool Eliminated { get; set; }

        [JsonProperty("chosenTowerLine")]
        public int ChosenTowerLine { get; set; }

        [JsonProperty("towerLineTiers")]
        public int[] TowerLineTiers { get; set; } = System.Array.Empty<int>();

        [JsonProperty("sendCategoryTiers")]
        public int[] SendCategoryTiers { get; set; } = System.Array.Empty<int>();
    }

    public sealed class TowerSnapshotDto
    {
        [JsonProperty("entityId")]
        public long EntityId { get; set; }

        [JsonProperty("towerId")]
        public string TowerId { get; set; } = "";

        [JsonProperty("ownerId")]
        public int OwnerId { get; set; }

        [JsonProperty("laneId")]
        public int LaneId { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }

        [JsonProperty("tier")]
        public int Tier { get; set; }
    }

    public sealed class CreepSnapshotDto
    {
        [JsonProperty("entityId")]
        public long EntityId { get; set; }

        [JsonProperty("creepId")]
        public string CreepId { get; set; } = "";

        [JsonProperty("senderId")]
        public int SenderId { get; set; }

        [JsonProperty("laneId")]
        public int LaneId { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }

        [JsonProperty("health")]
        public int Health { get; set; }

        [JsonProperty("maxHealth")]
        public int MaxHealth { get; set; }

        [JsonProperty("speedPerSecond")]
        public int SpeedPerSecond { get; set; }

        [JsonProperty("nextX")]
        public int NextX { get; set; }

        [JsonProperty("nextY")]
        public int NextY { get; set; }

        [JsonProperty("movementProgress")]
        public int MovementProgress { get; set; }

        [JsonProperty("effectiveMovementCost")]
        public int EffectiveMovementCost { get; set; }

        [JsonProperty("isBraked")]
        public bool IsBraked { get; set; }
    }

    public sealed class ErrorMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("message")]
        public string Message { get; set; } = "";
    }
}
