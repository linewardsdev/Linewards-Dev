using LTW.Simulation.Bots;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

public sealed class LocalMatchOptions
{
    public const int MinLaneCount = 2;
    public const int MaxLaneCount = 8;

    public LocalMatchOptions(
        int seed = 1,
        BotDecisionProfile player2Profile = BotDecisionProfile.Balanced,
        BotDecisionProfile player3Profile = BotDecisionProfile.Defensive,
        BotDecisionProfile player4Profile = BotDecisionProfile.Greedy,
        BotDecisionProfile player5Profile = BotDecisionProfile.Balanced,
        BotDecisionProfile player6Profile = BotDecisionProfile.Defensive,
        BotDecisionProfile player7Profile = BotDecisionProfile.Greedy,
        BotDecisionProfile player8Profile = BotDecisionProfile.Balanced,
        ContentId? player2PrimaryCreepId = null,
        ContentId? player3PrimaryCreepId = null,
        ContentId? player4PrimaryCreepId = null,
        ContentId? player5PrimaryCreepId = null,
        ContentId? player6PrimaryCreepId = null,
        ContentId? player7PrimaryCreepId = null,
        ContentId? player8PrimaryCreepId = null,
        int laneCount = MaxLaneCount)
    {
        Seed = seed;
        LaneCount = System.Math.Clamp(laneCount, MinLaneCount, MaxLaneCount);
        Player2Profile = player2Profile;
        Player3Profile = player3Profile;
        Player4Profile = player4Profile;
        Player5Profile = player5Profile;
        Player6Profile = player6Profile;
        Player7Profile = player7Profile;
        Player8Profile = player8Profile;
        Player2PrimaryCreepId = player2PrimaryCreepId;
        Player3PrimaryCreepId = player3PrimaryCreepId;
        Player4PrimaryCreepId = player4PrimaryCreepId;
        Player5PrimaryCreepId = player5PrimaryCreepId;
        Player6PrimaryCreepId = player6PrimaryCreepId;
        Player7PrimaryCreepId = player7PrimaryCreepId;
        Player8PrimaryCreepId = player8PrimaryCreepId;
    }

    public static LocalMatchOptions Default { get; } = new LocalMatchOptions();

    public int Seed { get; }

    public int LaneCount { get; }

    public BotDecisionProfile Player2Profile { get; }

    public BotDecisionProfile Player3Profile { get; }

    public BotDecisionProfile Player4Profile { get; }

    public BotDecisionProfile Player5Profile { get; }

    public BotDecisionProfile Player6Profile { get; }

    public BotDecisionProfile Player7Profile { get; }

    public BotDecisionProfile Player8Profile { get; }

    public ContentId? Player2PrimaryCreepId { get; }

    public ContentId? Player3PrimaryCreepId { get; }

    public ContentId? Player4PrimaryCreepId { get; }

    public ContentId? Player5PrimaryCreepId { get; }

    public ContentId? Player6PrimaryCreepId { get; }

    public ContentId? Player7PrimaryCreepId { get; }

    public ContentId? Player8PrimaryCreepId { get; }

    public BotDecisionProfile BotProfileFor(PlayerId playerId)
    {
        return playerId.Value switch
        {
            2 => Player2Profile,
            3 => Player3Profile,
            4 => Player4Profile,
            5 => Player5Profile,
            6 => Player6Profile,
            7 => Player7Profile,
            8 => Player8Profile,
            _ => BotDecisionProfile.Balanced
        };
    }

    public ContentId? PrimaryCreepFor(PlayerId playerId)
    {
        return playerId.Value switch
        {
            2 => Player2PrimaryCreepId,
            3 => Player3PrimaryCreepId,
            4 => Player4PrimaryCreepId,
            5 => Player5PrimaryCreepId,
            6 => Player6PrimaryCreepId,
            7 => Player7PrimaryCreepId,
            8 => Player8PrimaryCreepId,
            _ => null
        };
    }
}
