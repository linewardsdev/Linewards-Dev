using LTW.Simulation.Bots;
using LTW.Simulation.Content;

namespace LTW.Simulation.Bridge;

public sealed class LocalMatchOptions
{
    public LocalMatchOptions(
        int seed = 1,
        BotDecisionProfile player2Profile = BotDecisionProfile.Balanced,
        BotDecisionProfile player3Profile = BotDecisionProfile.Defensive,
        ContentId? player2PrimaryCreepId = null,
        ContentId? player3PrimaryCreepId = null)
    {
        Seed = seed;
        Player2Profile = player2Profile;
        Player3Profile = player3Profile;
        Player2PrimaryCreepId = player2PrimaryCreepId;
        Player3PrimaryCreepId = player3PrimaryCreepId;
    }

    public static LocalMatchOptions Default { get; } = new LocalMatchOptions();

    public int Seed { get; }

    public BotDecisionProfile Player2Profile { get; }

    public BotDecisionProfile Player3Profile { get; }

    public ContentId? Player2PrimaryCreepId { get; }

    public ContentId? Player3PrimaryCreepId { get; }
}
