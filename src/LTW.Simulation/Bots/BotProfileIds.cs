using LTW.Simulation.Content;

namespace LTW.Simulation.Bots;

/// <summary>
/// Canonical content IDs for each <see cref="BotDecisionProfile"/>'s tuning data, so
/// <see cref="BotController"/> can look up its own <see cref="BotProfileDefinition"/> from
/// <see cref="ContentCatalog.BotProfiles"/> without hardcoding a string id inline.
/// </summary>
public static class BotProfileIds
{
    public static readonly ContentId Greedy = new("bot.greedy");
    public static readonly ContentId Balanced = new("bot.balanced");
    public static readonly ContentId Defensive = new("bot.defensive");

    /// <summary>
    /// Optional in a catalog: a Passive bot that finds no entry under this id plays on Defensive's
    /// tuning — see <see cref="BotController.ResolveProfile"/>.
    /// </summary>
    public static readonly ContentId Passive = new("bot.passive");

    public static ContentId For(BotDecisionProfile profile) => profile switch
    {
        BotDecisionProfile.Greedy => Greedy,
        BotDecisionProfile.Balanced => Balanced,
        BotDecisionProfile.Defensive => Defensive,
        BotDecisionProfile.Passive => Passive,
        _ => Balanced
    };
}
