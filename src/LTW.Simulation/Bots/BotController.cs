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

    /// <summary>
    /// How long after sending a wall this bot will send something that needs one in front of it.
    /// </summary>
    /// <remarks>
    /// Nine ticks, and the number comes from the geometry rather than from feel. Every creep spawns
    /// at path index 0, a wall covers a cell every three ticks (speed 1 against
    /// <c>BaseMovementCost</c> 3), and a support's aura reaches
    /// <c>SupportAuraField.PathRadius</c> = 3 cells. So a support sent nine ticks after its wall
    /// spawns exactly at the edge of its own aura, and anything later spawns outside it — paying
    /// for an escort that can never catch what it was bought to help.
    ///
    /// The window is generous in one direction only: sending the escort SOONER is always better,
    /// because the support is slower than the wall and the gap only widens from there.
    /// </remarks>
    public const int EscortFollowWindowTicks = 9;

    /// <summary>Tick this bot last sent a wall, or null if it never has or has spent it.</summary>
    /// <remarks>
    /// The bot's whole notion of composition. It cannot see the lane — <see cref="Decide"/> receives
    /// an economy record and nothing else — so "is there a wall in front of this support" has to be
    /// answered from what it just bought rather than from what is on the board. That is a weaker
    /// signal than looking, but it is the right one here: a creep sent nine ticks ago IS still near
    /// the mouth of the lane, and no lane state can be consulted without widening the bot's
    /// interface to the whole simulation.
    ///
    /// Mutable per-bot state, which the replay determinism rules make worth stating: bots are
    /// rebuilt per match and decide in player-id order every tick, so the sequence of writes here is
    /// a pure function of the tick sequence.
    ///
    /// Nullable rather than a long.MinValue sentinel, and that is a bug fix rather than a style
    /// choice. With the sentinel, `tick.Value - lastWallSendTick` OVERFLOWS — at tick 300 it
    /// evaluates to -9223372036854775508, which is comfortably less than the nine-tick window, so
    /// the gate read as open at every tick a bot had never sent a wall. It silently did nothing,
    /// and the whole test suite passed with it in place.
    /// </remarks>
    private long? lastWallSendTick;

    public BotDecision Decide(PlayerEconomyState player, ContentCatalog content, SimulationTick tick)
    {
        var creep = SelectCreep(player, content, tick);
        var sendQuantity = GetSendQuantity(player, content, creep);
        if (sendQuantity <= 0)
        {
            return BotDecision.None;
        }

        if (IsWall(creep))
        {
            lastWallSendTick = tick.Value;
        }
        else
        {
            // Spent. One escort per wall, so a bot cannot answer a single wall with a stream of
            // supports — which would be the same mistake as sending them alone, just slower.
            lastWallSendTick = null;
        }

        return new BotDecision(new QueueSendCommand(player.PlayerId, tick, creep.Id, sendQuantity));
    }

    /// <summary>
    /// A creep that can carry a wave on its own: it walks the maze and buffs nobody.
    /// </summary>
    /// <remarks>
    /// The inverse — an "escort" — is anything whose value depends on other creeps being there.
    /// That is the four aura supports, whose buffs land on nothing when they travel alone, and
    /// Spire Turret Walker, which has 10 health and survives only while the towers are busy with
    /// somebody else. Both are wasted gold as an opening move, which is exactly what the bots did
    /// with them before this: they select by cost, and cost says nothing about needing company.
    /// </remarks>
    private static bool IsWall(CreepDefinition creep) =>
        creep.Support == CreepSupportRole.None && !creep.IgnoresMaze;

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

    private CreepDefinition SelectCreep(PlayerEconomyState player, ContentCatalog content, SimulationTick tick)
    {
        var available = player.Gold.Amount - GoldReserveFloor(content);
        var income = player.Income.Amount;
        // Category 1 (creep.wisp/.revenant/.obsidian_brute/.serpent/.turret_walker) was slotted in
        // when it was still a stat tier — Greedy leaned on Turret Walker and Revenant for income
        // efficiency and Wisp as a cheap opener, Balanced and Defensive on Obsidian Brute and
        // Serpent as tankier alternatives to Brute. None of those reasons survive the SUPPORT
        // rework: those five are escorts now, and their membership here is what makes them
        // available AFTER a wall rather than instead of one.
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
        // Category 2 before it. Costs as of the SUPPORT rework: colossus 52, siege 40, warden 34,
        // obsidian_brute 30, stalker 28, serpent 27, burrower 26, shade 24, turret_walker 23,
        // zephyr 22, revenant 19, brute 18, wisp 12, runner 10, swarm 6. Greedy takes the expensive
        // top end where its income allows; Balanced and Defensive gain the two mid-tier tanks
        // (warden, burrower) that suit their health-per-gold bias.
        //
        // Membership still says nothing about composition, which is what the escort window above
        // adds. Five of the ids these lists name — wisp, revenant, obsidian_brute, serpent and
        // turret_walker — stopped being bodies when category 1 became SUPPORT, and until the window
        // existed the bots kept buying them as though they still were.
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

        var affordable = preferredIds
            .Distinct()
            .Select(id => content.Creeps.FirstOrDefault(creep => creep.Id.Value == id))
            .Where(creep => creep != null && available >= creep.Cost.Amount)
            .OrderByDescending(creep => creep!.Cost.Amount)
            .ThenBy(creep => creep!.Id.Value, System.StringComparer.Ordinal)
            .ToArray();

        // Having just sent a wall, ESCORT it. Gating escorts was necessary and turned out not to be
        // sufficient: the sort above takes the dearest creep a bot can afford, and walls are the
        // dear ones, so a rich bot opened the escort window and then spent it on another Colossus
        // every time. Escorts were only ever reached when the bot was too poor for a wall, which is
        // the exact opposite of when they are worth sending. Preferring them inside the window is
        // what actually produces wall-then-escort rather than merely permitting it.
        var escortsAllowed = lastWallSendTick is { } sentAt && tick.Value - sentAt <= EscortFollowWindowTicks;
        if (escortsAllowed)
        {
            var escort = affordable.FirstOrDefault(creep => !IsWall(creep!));
            if (escort is not null)
            {
                return escort;
            }
        }

        // Otherwise a wall — and specifically a wall, not just "the next thing down the list".
        // Falling back to any affordable creep here would let an unescorted support through on the
        // ticks a bot cannot afford a body, which is the behaviour the window exists to stop.
        foreach (var candidate in affordable)
        {
            if (IsWall(candidate!))
            {
                return candidate!;
            }
        }

        // Throws with a clear message rather than a bare "sequence contains no matching element" if
        // this bot's configured primary creep isn't in content — matching the same "detect missing
        // content before play" principle as ResolveProfile above. BotLaneOptions.PrimaryCreepId is
        // never validated at construction time, so this is the first point a typo would surface.
        return content.Creeps.FirstOrDefault(creep => creep.Id.Equals(creepId))
            ?? throw new InvalidOperationException($"No CreepDefinition found for this bot's configured primary creep '{creepId.Value}'.");
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

        // An aura is on or off, so a second copy of one buys nothing — two Menders heal a creep
        // once. Sending the profile's usual batch of three would be three times the price for the
        // same effect, which is the sort of waste that makes a category look weak when it is really
        // just being bought wrong. Spire Turret Walker is deliberately NOT included: it carries no
        // aura, so more of them really is more pressure.
        if (creep.Support != CreepSupportRole.None)
        {
            return 1;
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
