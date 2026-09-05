#nullable enable

using Newtonsoft.Json;

namespace LTW.UnityClient.Online.Wire
{
    /// <summary>
    /// Client-side mirror of LTW.MatchServer/Wire/ClientMessages.cs. Kept as plain classes with a
    /// literal "type" string per concrete class, because the server reads that field BEFORE
    /// deciding which shape to deserialize the rest of the frame into — see ServerMatch.DispatchAsync's
    /// switch on the raw JSON "type" property. The type strings below ("placeTower", "queueSend", ...)
    /// must match that switch exactly; they are not derived from anything shared, because nothing is
    /// shared between the two codebases at build time.
    /// </summary>
    public abstract class ClientCommandMessage
    {
        [JsonProperty("id")]
        public string? Id { get; set; }
    }

    public sealed class PlaceTowerMessage : ClientCommandMessage
    {
        [JsonProperty("type")]
        public string Type => "placeTower";

        [JsonProperty("laneId")]
        public int LaneId { get; set; }

        [JsonProperty("towerId")]
        public string TowerId { get; set; } = "";

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

    /// <summary>
    /// An immediate, all-or-nothing spend and spawn — rejected outright if the sender cannot
    /// afford it right now. NOT what the real send-dock UI should use; see
    /// <see cref="EnqueueSendMessage"/> for the actual send queue.
    /// </summary>
    public sealed class QueueSendMessage : ClientCommandMessage
    {
        [JsonProperty("type")]
        public string Type => "queueSend";

        [JsonProperty("creepId")]
        public string CreepId { get; set; } = "";

        [JsonProperty("quantity")]
        public int Quantity { get; set; } = 1;
    }

    /// <summary>
    /// Adds one creep to the sender's send queue, drained by the simulation as gold becomes
    /// available — what a real player's tap of a send-dock card means locally
    /// (<c>UnityCommandAdapter.SendCreep</c>'s own remarks). Found missing live, testing a real
    /// online match: <see cref="UnityCommandAdapter.SendCreep"/> sent <see cref="QueueSendMessage"/>
    /// instead, so every online send silently failed the instant the sender could not afford it
    /// immediately, instead of waiting like local play does.
    /// </summary>
    public sealed class EnqueueSendMessage : ClientCommandMessage
    {
        [JsonProperty("type")]
        public string Type => "enqueueSend";

        [JsonProperty("creepId")]
        public string CreepId { get; set; } = "";
    }

    public sealed class BuyCategoryTierMessage : ClientCommandMessage
    {
        [JsonProperty("type")]
        public string Type => "buyCategoryTier";

        /// <summary>0 = TowerLine, 1 = SendCategory — matches LTW.Simulation.Commands.CategoryKind's declaration order.</summary>
        [JsonProperty("categoryKind")]
        public int CategoryKind { get; set; }

        [JsonProperty("categoryIndex")]
        public int CategoryIndex { get; set; }

        [JsonProperty("targetTier")]
        public int TargetTier { get; set; }
    }

    public sealed class UpgradeTowerMessage : ClientCommandMessage
    {
        [JsonProperty("type")]
        public string Type => "upgradeTower";

        [JsonProperty("laneId")]
        public int LaneId { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

    public sealed class SellTowerMessage : ClientCommandMessage
    {
        [JsonProperty("type")]
        public string Type => "sellTower";

        [JsonProperty("laneId")]
        public int LaneId { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }
}
