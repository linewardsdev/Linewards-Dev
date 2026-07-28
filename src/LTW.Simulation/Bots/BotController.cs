using System;
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
        var creep = SelectCreep(player, content);
        var sendQuantity = GetSendQuantity(player, content, creep);
        return sendQuantity > 0
            ? new BotDecision(new QueueSendCommand(player.PlayerId, tick, creep.Id, sendQuantity))
            : BotDecision.None;
    }

    /// <summary>
    /// Reads this profile's tuning data from content instead of a hardcoded constant, so balance
    /// changes (tower costs, creep costs) don't silently desync the bot's reserve/coverage/
    /// pressure behavior from the numbers it was tuned against. Throws rather than falling back
    /// silently, matching the project's existing "detect missing content before play" principle
    /// (see docs/ARCHITECTURE.md's Content And Persistence section) — a missing profile entry is a
    /// content authoring bug, not a runtime condition to paper over.
    /// </summary>
    public BotProfileDefinition ResolveProfile(ContentCatalog content)
    {
        var id = BotProfileIds.For(profile);
        return content.BotProfiles.FirstOrDefault(candidate => candidate.Id.Equals(id))
            ?? throw new InvalidOperationException($"No BotProfileDefinition found for '{id.Value}'. Every BotDecisionProfile needs a matching content entry.");
    }

    public int GoldReserveFloor(ContentCatalog content) => ResolveProfile(content).MinimumGoldReserve;

    /// <summary>
    /// Higher defense bias means the bot tolerates less incoming pressure before it stops sending
    /// and holds/builds instead — this is the reactive equivalent of the old tick-scheduled
    /// "build defense before sending" behavior, but driven by actual lane threat rather than tick
    /// number.
    /// </summary>
    /// <remarks>
    /// Scales up with the bot's own tower count so tolerance grows alongside its actual defense
    /// capacity. Without this, a bot facing a sustained-aggressive neighbor could hit the
    /// threshold once, stay there indefinitely (more towers doesn't inherently clear an existing
    /// creep backlog faster than new pressure arrives), and never send again for the rest of the
    /// match — caught by <c>GameplayScenarioTests.Mixed_pressure_scenario_records_distinct_send_roles_and_defensive_response</c>,
    /// where a flat threshold left a 4-tower Balanced bot permanently locked out of sending.
    /// </remarks>
    public int PressureThreshold(ContentCatalog content, int ownedTowerCount)
    {
        var defenseBias = ResolveProfile(content).DefenseBias;
        var baseThreshold = System.Math.Max(10, 120 - defenseBias);
        return baseThreshold + ownedTowerCount * 15;
    }

    private CreepDefinition SelectCreep(PlayerEconomyState player, ContentCatalog content)
    {
        var available = player.Gold.Amount - GoldReserveFloor(content);
        var income = player.Income.Amount;
        var preferredIds = profile switch
        {
            BotDecisionProfile.Greedy => income >= 45
                ? new[] { "creep.siege", "creep.shade", "creep.brute", creepId.Value }
                : income >= 20
                    ? new[] { "creep.shade", "creep.brute", creepId.Value }
                    : new[] { creepId.Value, "creep.brute" },
            BotDecisionProfile.Balanced => income >= 35
                ? new[] { "creep.shade", "creep.brute", "creep.swarm", creepId.Value }
                : new[] { "creep.brute", "creep.swarm", creepId.Value },
            BotDecisionProfile.Defensive => income >= 30
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

    private int GetSendQuantity(PlayerEconomyState player, ContentCatalog content, CreepDefinition creep)
    {
        if (player.IsEliminated)
        {
            return 0;
        }

        var reserve = GoldReserveFloor(content);

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
