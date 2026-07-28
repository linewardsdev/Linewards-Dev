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

    private static readonly BotLaneOptions[] DefaultLanes =
    {
        new(2, profile: BotDecisionProfile.Balanced),
        new(3, profile: BotDecisionProfile.Defensive),
        new(4, profile: BotDecisionProfile.Greedy),
        new(5, profile: BotDecisionProfile.Greedy),
        new(6, profile: BotDecisionProfile.Greedy),
        new(7, profile: BotDecisionProfile.Greedy),
        new(8, profile: BotDecisionProfile.Greedy)
    };

    private readonly IReadOnlyDictionary<int, BotLaneOptions> lanes;

    public LocalMatchOptions(int seed = 1, int laneCount = MaxLaneCount, IReadOnlyList<BotLaneOptions>? botLanes = null)
    {
        Seed = seed;
        LaneCount = System.Math.Clamp(laneCount, MinLaneCount, MaxLaneCount);

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

        return new LocalMatchOptions(Seed, LaneCount, lanes.Values.Where(lane => lane.PlayerId.Value != playerId).Append(overridden).ToArray());
    }

    public bool IsBotEnabledFor(PlayerId playerId) =>
        lanes.TryGetValue(playerId.Value, out var lane) && lane.Enabled;

    public BotDecisionProfile BotProfileFor(PlayerId playerId) =>
        lanes.TryGetValue(playerId.Value, out var lane) ? lane.Profile : BotDecisionProfile.Balanced;

    public ContentId? PrimaryCreepFor(PlayerId playerId) =>
        lanes.TryGetValue(playerId.Value, out var lane) ? lane.PrimaryCreepId : null;
}
