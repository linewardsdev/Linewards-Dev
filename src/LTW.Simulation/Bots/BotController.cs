using System.Linq;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Bots;

public sealed class BotController
{
    private readonly BotDecisionProfile profile;
    private readonly ContentId creepId;

    public BotController(BotDecisionProfile profile, ContentId creepId)
    {
        this.profile = profile;
        this.creepId = creepId;
    }

    public BotDecisionProfile Profile => profile;

    public ContentId PrimaryCreepId => creepId;

    public BotDecision Decide(PlayerEconomyState player, ContentCatalog content, SimulationTick tick)
    {
        var creep = content.Creeps.First(creep => creep.Id.Equals(creepId));
        var sendQuantity = GetSendQuantity(player, creep, tick);
        return sendQuantity > 0
            ? new BotDecision(new QueueSendCommand(player.PlayerId, tick, creepId, sendQuantity))
            : BotDecision.None;
    }

    private int GetSendQuantity(PlayerEconomyState player, CreepDefinition creep, SimulationTick tick)
    {
        if (player.IsEliminated)
        {
            return 0;
        }

        var reserve = profile switch
        {
            BotDecisionProfile.Greedy => 0,
            BotDecisionProfile.Balanced => tick.Value < 120 ? 70 : 35,
            BotDecisionProfile.Defensive => tick.Value < 180 ? 85 : 55,
            _ => 20
        };

        var available = player.Gold.Amount - reserve;
        if (available < creep.Cost.Amount)
        {
            return 0;
        }

        var max = available / creep.Cost.Amount;
        return profile switch
        {
            BotDecisionProfile.Greedy => System.Math.Min(3, max),
            BotDecisionProfile.Balanced => System.Math.Min(2, max),
            BotDecisionProfile.Defensive => 1,
            _ => 1
        };
    }
}
