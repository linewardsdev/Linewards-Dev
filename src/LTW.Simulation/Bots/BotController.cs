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
        // Category 2 (creep.wisp/.revenant/.obsidian_brute/.serpent/.turret_walker, added
        // 2026-07-28) slotted in by matching each profile's existing character rather than just
        // appended: Greedy leans on Turret Walker/Revenant for their income efficiency and Wisp
        // as a cheap opener; Balanced and Defensive lean on Obsidian Brute/Serpent for their
        // health-per-gold as tankier alternatives to Brute.
        //
        // These lists express *membership* — which creeps a profile is willing to send in a given
        // income tier. Ordering is not a preference and is no longer authored by hand: the list is
        // sorted by descending cost below before it is used.
        //
        // That sort is load-bearing. The loop tries each id in order and returns the first one
        // `available` gold covers, so any id sitting after a cheaper one is mathematically
        // unreachable — affording the cheaper one never implies being unable to afford the pricier
        // one, so the cheaper entry always wins first. An early hand-ordered version left
        // creep.brute/.shade/.siege/.serpent/.obsidian_brute at zero sends across an entire batch
        // playtest while creep.revenant alone took 207 of 486, and it was only caught by replay
        // analysis. Hand-ordering also silently broke `creepId.Value`, the bot's configured
        // primary creep, which was appended last regardless of price: a bot given the 40-gold
        // Siege as its primary could never actually send it from a tier whose other entries were
        // cheaper. Sorting makes the invariant structural instead of a comment to be honoured.
        // Category 3 ("ELITE", added 2026-07-28) slotted in at cost-sorted positions, same as
        // Category 2 before it. Costs for reference: colossus 52, walker 38, warden 34, siege 40,
        // obsidian_brute 30, stalker 28, burrower 26, shade 24, zephyr 22, serpent 20, brute 18,
        // revenant 16, runner 10, swarm 6, wisp 5. Greedy takes the expensive top end where its
        // income allows; Balanced and Defensive gain the two mid-tier tanks (warden, burrower)
        // that suit their health-per-gold bias.
        var preferredIds = profile switch
        {
            BotDecisionProfile.Greedy => income >= 45
                ? new[] { "creep.colossus", "creep.siege", "creep.turret_walker", "creep.warden", "creep.stalker", "creep.shade", "creep.brute", "creep.revenant", creepId.Value }
                : income >= 20
                    ? new[] { "creep.stalker", "creep.shade", "creep.zephyr", "creep.brute", "creep.revenant", creepId.Value }
                    : new[] { "creep.brute", creepId.Value, "creep.wisp" },
            BotDecisionProfile.Balanced => income >= 35
                ? new[] { "creep.warden", "creep.obsidian_brute", "creep.burrower", "creep.shade", "creep.brute", creepId.Value, "creep.swarm" }
                : new[] { "creep.serpent", "creep.brute", creepId.Value, "creep.swarm" },
            BotDecisionProfile.Defensive => income >= 30
                ? new[] { "creep.warden", "creep.obsidian_brute", "creep.burrower", "creep.serpent", "creep.brute", "creep.runner", creepId.Value, "creep.swarm" }
                : new[] { "creep.runner", creepId.Value, "creep.swarm", "creep.wisp" },
            _ => new[] { creepId.Value }
        };

        var candidates = preferredIds
            .Distinct()
            .Select(id => content.Creeps.FirstOrDefault(creep => creep.Id.Value == id))
            .Where(creep => creep != null)
            .OrderByDescending(creep => creep!.Cost.Amount)
            .ThenBy(creep => creep!.Id.Value, System.StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (available >= candidate!.Cost.Amount)
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
