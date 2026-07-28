using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bots;

/// <summary>
/// Per-lane bot configuration: whether a lane is bot-controlled at all, and if so which profile
/// and primary creep it uses. Passed into <see cref="Bridge.LocalMatchOptions"/> to override one
/// lane's defaults without needing to specify every other lane.
/// </summary>
public sealed class BotLaneOptions
{
    public BotLaneOptions(
        int playerId,
        bool enabled = true,
        BotDecisionProfile profile = BotDecisionProfile.Balanced,
        ContentId? primaryCreepId = null)
    {
        PlayerId = new PlayerId(playerId);
        Enabled = enabled;
        Profile = profile;
        PrimaryCreepId = primaryCreepId;
    }

    public PlayerId PlayerId { get; }

    public bool Enabled { get; }

    public BotDecisionProfile Profile { get; }

    public ContentId? PrimaryCreepId { get; }
}
