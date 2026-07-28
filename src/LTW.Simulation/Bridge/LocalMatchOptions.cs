using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

public sealed class LocalMatchOptions
{
    public const int MinLaneCount = 2;
    public const int MaxLaneCount = 8;

    // Player 1 has an entry here even though it is the default human seat: the entry only takes
    // effect when the human is seated elsewhere (see IsBotEnabledFor), so moving the local seat to
    // lane 3 leaves lane 1 bot-driven rather than idle.
    private static readonly BotLaneOptions[] DefaultLanes =
    {
        new(1, profile: BotDecisionProfile.Balanced),
        new(2, profile: BotDecisionProfile.Balanced),
        new(3, profile: BotDecisionProfile.Defensive),
        new(4, profile: BotDecisionProfile.Greedy),
        new(5, profile: BotDecisionProfile.Greedy),
        new(6, profile: BotDecisionProfile.Greedy),
        new(7, profile: BotDecisionProfile.Greedy),
        new(8, profile: BotDecisionProfile.Greedy)
    };

    private readonly IReadOnlyDictionary<int, BotLaneOptions> lanes;

    public LocalMatchOptions(int seed = 1, int laneCount = MaxLaneCount, IReadOnlyList<BotLaneOptions>? botLanes = null, int localPlayerId = 1)
    {
        Seed = seed;
        LaneCount = System.Math.Clamp(laneCount, MinLaneCount, MaxLaneCount);
        LocalPlayerId = new PlayerId(System.Math.Clamp(localPlayerId, 1, LaneCount));

        var merged = DefaultLanes.ToDictionary(lane => lane.PlayerId.Value);
        if (botLanes != null)
        {
            foreach (var lane in botLanes)
            {
                merged[lane.PlayerId.Value] = lane;
            }
        }

        lanes = merged;
    }

    public static LocalMatchOptions Default { get; } = new LocalMatchOptions();

    public int Seed { get; }

    public int LaneCount { get; }

    /// <summary>
    /// Which seat the local human occupies. Previously this was an unstated assumption spread
    /// across the Unity client as hardcoded <c>new PlayerId(1)</c> / <c>new LaneId(1)</c> literals;
    /// naming it here is the first step toward remote players, where each client drives a different
    /// seat of the same match. The seat's own lane is always <c>topology.HomeLaneFor(LocalPlayerId)</c>.
    /// </summary>
    public PlayerId LocalPlayerId { get; }

    /// <summary>
    /// Returns a copy of these options with the local human seated in a different lane. Whichever
    /// lane the human occupies stops being bot-driven, and the lane they vacate starts being
    /// bot-driven, without needing to restate any lane configuration.
    /// </summary>
    public LocalMatchOptions WithLocalPlayer(int playerId) =>
        new LocalMatchOptions(Seed, LaneCount, lanes.Values.ToArray(), playerId);

    /// <summary>
    /// Returns a copy of these options with a single lane's bot configuration overridden, leaving
    /// every other lane untouched. The usual way to express "start from the defaults, but change
    /// lane N" without needing to restate every other lane.
    /// </summary>
    public LocalMatchOptions WithLane(int playerId, bool? enabled = null, BotDecisionProfile? profile = null, ContentId? primaryCreepId = null)
    {
        var current = lanes.TryGetValue(playerId, out var existing) ? existing : new BotLaneOptions(playerId);
        var overridden = new BotLaneOptions(
            playerId,
            enabled ?? current.Enabled,
            profile ?? current.Profile,
            primaryCreepId ?? current.PrimaryCreepId);

        return new LocalMatchOptions(Seed, LaneCount, lanes.Values.Where(lane => lane.PlayerId.Value != playerId).Append(overridden).ToArray(), LocalPlayerId.Value);
    }

    /// <summary>
    /// The local human's own seat is never bot-driven, whatever the lane config says. This keeps
    /// "who is the human" a single knob (<see cref="LocalPlayerId"/>) instead of requiring callers
    /// to remember to also disable the bot on that lane.
    /// </summary>
    public bool IsBotEnabledFor(PlayerId playerId) =>
        !playerId.Equals(LocalPlayerId) &&
        lanes.TryGetValue(playerId.Value, out var lane) && lane.Enabled;

    public BotDecisionProfile BotProfileFor(PlayerId playerId) =>
        lanes.TryGetValue(playerId.Value, out var lane) ? lane.Profile : BotDecisionProfile.Balanced;

    public ContentId? PrimaryCreepFor(PlayerId playerId) =>
        lanes.TryGetValue(playerId.Value, out var lane) ? lane.PrimaryCreepId : null;
}
