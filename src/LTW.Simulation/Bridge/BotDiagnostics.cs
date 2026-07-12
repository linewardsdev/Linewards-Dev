using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Bots;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bridge;

public sealed class BotProfileSnapshot
{
    public BotProfileSnapshot(PlayerId playerId, BotDecisionProfile profile, ContentId primaryCreepId)
    {
        PlayerId = playerId;
        Profile = profile;
        PrimaryCreepId = primaryCreepId;
    }

    public PlayerId PlayerId { get; }

    public BotDecisionProfile Profile { get; }

    public ContentId PrimaryCreepId { get; }
}

public sealed class BotDecisionRecord
{
    public BotDecisionRecord(SimulationTick tick, PlayerId playerId, BotDecisionProfile profile, ContentId contentId, int quantity)
    {
        Tick = tick;
        PlayerId = playerId;
        Profile = profile;
        ContentId = contentId;
        Quantity = quantity;
    }

    public SimulationTick Tick { get; }

    public PlayerId PlayerId { get; }

    public BotDecisionProfile Profile { get; }

    public ContentId ContentId { get; }

    public int Quantity { get; }
}

public sealed class BotDiagnosticsSnapshot
{
    public BotDiagnosticsSnapshot(IReadOnlyList<BotProfileSnapshot> profiles, IReadOnlyList<BotDecisionRecord> recentDecisions)
    {
        Profiles = profiles.ToArray();
        RecentDecisions = recentDecisions.ToArray();
    }

    public IReadOnlyList<BotProfileSnapshot> Profiles { get; }

    public IReadOnlyList<BotDecisionRecord> RecentDecisions { get; }
}
