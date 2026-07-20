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
        var creep = SelectCreep(player, content, tick);
        var sendQuantity = GetSendQuantity(player, creep, tick);
        return sendQuantity > 0
            ? new BotDecision(new QueueSendCommand(player.PlayerId, tick, creep.Id, sendQuantity))
            : BotDecision.None;
    }

    private CreepDefinition SelectCreep(PlayerEconomyState player, ContentCatalog content, SimulationTick tick)
    {
        var available = player.Gold.Amount - GoldReserve(tick);
        var preferredIds = profile switch
        {
            BotDecisionProfile.Greedy => tick.Value >= 260
                ? new[] { "creep.siege", "creep.shade", "creep.brute", creepId.Value }
                : tick.Value >= 120
                    ? new[] { "creep.shade", "creep.brute", creepId.Value }
                    : new[] { creepId.Value, "creep.brute" },
            BotDecisionProfile.Balanced => tick.Value >= 180
                ? new[] { "creep.shade", "creep.brute", "creep.swarm", creepId.Value }
                : new[] { "creep.brute", "creep.swarm", creepId.Value },
            BotDecisionProfile.Defensive => tick.Value >= 220
                ? new[] { "creep.brute", "creep.swarm", "creep.runner", creepId.Value }
                : new[] { "creep.swarm", "creep.runner", creepId.Value },
            _ => new[] { creepId.Value }
        };

        foreach (var preferredId in preferredIds)
        {
            var candidate = content.Creeps.FirstOrDefault(creep => creep.Id.Value == preferredId);
            if (candidate != null && available >= candidate.Cost.Amount)
            {
                return candidate;
            }
        }

        return content.Creeps.First(creep => creep.Id.Equals(creepId));
    }

    private int GetSendQuantity(PlayerEconomyState player, CreepDefinition creep, SimulationTick tick)
    {
        if (player.IsEliminated)
        {
            return 0;
        }

        var reserve = GoldReserve(tick);

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

    private int GoldReserve(SimulationTick tick)
    {
        return profile switch
        {
            BotDecisionProfile.Greedy => 0,
            BotDecisionProfile.Balanced => tick.Value < 120 ? 80 : 35,
            BotDecisionProfile.Defensive => tick.Value < 180 ? 85 : 30,
            _ => 20
        };
    }
}
