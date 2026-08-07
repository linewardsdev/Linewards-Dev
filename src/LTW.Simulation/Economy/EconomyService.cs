using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Economy;

public sealed class EconomyService
{
    private readonly EconomyRules rules;

    public EconomyService(EconomyRules rules)
    {
        this.rules = rules;
    }

    /// <summary>Exposed so presentation layers can derive an income countdown without duplicating
    /// this number (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items — the Unity client had its own hardcoded copy).</summary>
    public int IncomeIntervalTicks => rules.IncomeIntervalTicks;

    public bool IsIncomeTick(SimulationTick tick) =>
        tick.Value > 0 && tick.Value % rules.IncomeIntervalTicks == 0;

    public EconomyPlayerSet ApplyIncomeTick(EconomyPlayerSet players, SimulationTick tick)
    {
        if (!IsIncomeTick(tick))
        {
            return players;
        }

        var next = players;
        foreach (var player in players.Players.Where(player => !player.IsEliminated))
        {
            var updated = player.WithGold(new Gold(player.Gold.Amount + player.Income.Amount));
            next = next.Replace(updated);
        }

        return next;
    }

    public SendResult QueueSend(
        EconomyPlayerSet players,
        PlayerId senderId,
        CreepDefinition creep,
        int quantity,
        SimulationTick requestedTick)
    {
        var targetId = players.GetCarouselTarget(senderId);
        return QueueSend(players, senderId, creep, quantity, requestedTick, targetId);
    }

    public SendResult QueueSend(
        EconomyPlayerSet players,
        PlayerId senderId,
        CreepDefinition creep,
        int quantity,
        SimulationTick requestedTick,
        PlayerId targetId)
    {
        if (quantity <= 0)
        {
            return SendResult.Reject(players, CommandRejectionReason.InvalidQuantity);
        }

        var sender = players.Get(senderId);
        if (sender.IsEliminated)
        {
            return SendResult.Reject(players, CommandRejectionReason.PlayerEliminated);
        }

        if (targetId.Equals(senderId) || players.Get(targetId).IsEliminated)
        {
            return SendResult.Reject(players, CommandRejectionReason.InvalidPlayer);
        }

        // Send cooldown, enforced here for the first time. EconomyRules.SendCooldownTicks,
        // PlayerEconomyState.NextSendAvailableTick and WithNextSendAvailableTick all already
        // existed, and CommandRejectionReason.CooldownActive was already defined, but nothing
        // ever read or set any of them — the "global 30-tick send cooldown" the design docs and
        // GD_TUNING_LOG have been describing was never actually running, leaving sends limited
        // only by gold. That is a rate-limit hole for remote clients specifically (see
        // ARCHITECTURE.md's "rate limits and command cooldowns are enforced server-side").
        // Category 2 creeps are exempt: they send whenever the player can afford them. The
        // exemption is declared on the creep (CreepDefinition.IgnoresSendCooldown) rather than
        // matched against ids here.
        if (!creep.IgnoresSendCooldown && requestedTick.Value < sender.NextSendAvailableTick.Value)
        {
            return SendResult.Reject(players, CommandRejectionReason.CooldownActive);
        }

        // Priced at the sender's tier for this creep's category, not the authored cost. A tier
        // raises the health of everything in the category, and until now raised nothing about what
        // it charged — so the tier paid for itself once and every send after it was free power.
        var cost = SendCostFor(sender, creep, quantity);
        if (sender.Gold.Amount < cost)
        {
            return SendResult.Reject(players, CommandRejectionReason.InsufficientGold);
        }

        var incomeGained = IncomeGainFor(sender.Income, creep, quantity);
        var updatedSender = sender
            .WithGold(new Gold(sender.Gold.Amount - cost))
            .WithIncome(new Income(Math.Min(rules.IncomeCeiling, sender.Income.Amount + incomeGained)));

        // An exempt send does not arm the cooldown either. Arming it would let a Category 2 send
        // gate the next Category 1 send, which is not what "send at any time" means, and would
        // make spamming the cheapest exempt creep a way to lock out everything else.
        if (!creep.IgnoresSendCooldown)
        {
            updatedSender = updatedSender
                .WithNextSendAvailableTick(new SimulationTick(requestedTick.Value + rules.SendCooldownTicks));
        }

        return SendResult.Accept(players.Replace(updatedSender), targetId);
    }

    /// <summary>
    /// What <paramref name="quantity"/> of <paramref name="creep"/> costs this sender right now.
    /// </summary>
    /// <remarks>
    /// Public because the send dock has to show it. A card quoting the authored cost while the
    /// economy charges the tiered one is a button that looks affordable and is refused, and the gap
    /// widens with every tier bought — the same failure the category tier cards were already
    /// written to avoid.
    ///
    /// The multiplier applies to the unit price and the quantity multiplies the result, so a bulk
    /// send is priced exactly as the same number of single sends. Doing it the other way rounds
    /// once per batch instead of once per creep and makes quantity a cheap way to shave gold.
    /// </remarks>
    public int SendCostFor(PlayerEconomyState sender, CreepDefinition creep, int quantity)
    {
        var tier = sender.SendCategoryTier(creep.CategoryIndex);
        var unitCost = creep.Cost.Amount * CategoryTierRules.SendCostPercentFor(tier) / 100;
        return unitCost * quantity;
    }

    /// <summary>
    /// The income a send actually grants, tapered toward zero as income approaches the ceiling.
    /// </summary>
    /// <remarks>
    /// Exposed so presentation can show the player what a send is really worth right now. The nominal
    /// <see cref="CreepDefinition.IncomeGain"/> stops being the true number once a player is climbing
    /// the taper, and a send button advertising +5 while granting +2 is worse than no number at all.
    ///
    /// Below <see cref="EconomyRules.IncomeTaperStart"/> this returns the nominal gain unchanged, so
    /// the opening and midgame are exactly as they were. Across the band above it the taper scales
    /// linearly with remaining headroom, which keeps it proportional rather than a per-creep rule: a
    /// creep worth five times another is still worth five times as much at every income level.
    ///
    /// Rounding is deliberately UP, so the cheapest gain-1 creeps keep granting their full 1 across
    /// the whole band instead of silently becoming worthless at the first reduction — rounding down
    /// would zero them out halfway up and quietly delete the low end of the roster, which is the same
    /// failure the stat rebalance was fixing.
    /// </remarks>
    public int IncomeGainFor(Income currentIncome, CreepDefinition creep, int quantity)
    {
        var nominal = creep.IncomeGain.Amount * quantity;
        if (nominal <= 0)
        {
            return 0;
        }

        var headroom = rules.IncomeCeiling - currentIncome.Amount;
        if (headroom <= 0)
        {
            return 0;
        }

        if (currentIncome.Amount <= rules.IncomeTaperStart)
        {
            return Math.Min(nominal, headroom);
        }

        // Integer ceiling division of (nominal * headroom) / band. At the knee headroom equals the
        // band width, so this returns the nominal gain and the curve joins continuously.
        var band = rules.IncomeCeiling - rules.IncomeTaperStart;
        var tapered = (nominal * headroom + band - 1) / band;
        return Math.Min(Math.Max(1, tapered), headroom);
    }

    public EconomyResult ApplyKillBounty(EconomyPlayerSet players, PlayerId defenderId, CreepDefinition creep)
    {
        var defender = players.Get(defenderId);
        if (defender.IsEliminated)
        {
            return EconomyResult.Reject(players, CommandRejectionReason.PlayerEliminated);
        }

        return EconomyResult.Accept(players.Replace(defender.WithGold(new Gold(defender.Gold.Amount + creep.KillBounty.Amount))));
    }

    public LeakResult ApplyLeak(EconomyPlayerSet players, PlayerId senderId, PlayerId defenderId, CreepDefinition creep)
    {
        return ApplyLeak(players, senderId, defenderId, creep, new Lives(rules.LeakLifeLoss));
    }

    public LeakResult ApplyLeak(EconomyPlayerSet players, PlayerId senderId, PlayerId defenderId, CreepDefinition creep, Lives requestedLivesLost)
    {
        var sender = players.Get(senderId);
        var defender = players.Get(defenderId);
        var livesLost = Math.Min(defender.Lives.Amount, Math.Max(1, requestedLivesLost.Amount));
        var updatedDefender = defender.WithLives(new Lives(defender.Lives.Amount - livesLost));

        // A leak into the sender's own lane credits nothing and must not touch the sender record at
        // all. The two Replace calls below apply to the same player when the ids match, and the
        // second one is built from the PRE-leak snapshot — so writing it would silently undo the
        // life loss. The carousel is supposed to prevent a player's own creeps re-entering their
        // lane, which is exactly why this is a cheap guard rather than a tested-for scenario.
        if (senderId.Equals(defenderId))
        {
            return new LeakResult(players.Replace(updatedDefender), new Lives(livesLost), creep.LeakBounty, new Lives(0));
        }

        // An eliminated sender's own creep can still be mid-lane and leak after they're already out
        // of the match (queued before elimination), so the defender still takes the lives loss — but
        // ApplyKillBounty already refuses to pay out against an eliminated participant, and crediting
        // gold to a sender who is out of the game is the same kind of no-op payout, just on the other
        // side of the transaction (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items). LeakResult still reports the creep's
        // nominal LeakBounty either way, matching the LeakEvent CombatService already raised for this
        // same leak (computed independently, before elimination status is known here) — only the
        // actual gold credit is suppressed.
        var goldCredited = sender.IsEliminated ? 0 : creep.LeakBounty.Amount;

        // Lives are STOLEN, not destroyed: what the defender loses, the sender gains, so the total
        // in play is conserved and a leak moves the win condition rather than only eroding it.
        //
        // Deliberately uncapped — a sender can exceed the starting count. Capping would break the
        // conservation the mechanic is built on and would silently make late steals worthless,
        // which is the opposite of the intent.
        //
        // Suppressed for an eliminated sender on the same reasoning as the gold above: crediting a
        // player who is out of the match is a no-op payout, and here it would be worse than a no-op
        // because lives are the win condition — a dead player accumulating them could un-eliminate
        // themselves depending on how elimination is later re-evaluated.
        var livesStolen = sender.IsEliminated ? 0 : livesLost;

        var updatedSender = sender
            .WithGold(new Gold(sender.Gold.Amount + goldCredited))
            .WithLives(new Lives(sender.Lives.Amount + livesStolen));

        var next = players.Replace(updatedDefender).Replace(updatedSender);
        return new LeakResult(next, new Lives(livesLost), creep.LeakBounty, new Lives(livesStolen));
    }

    public Gold CalculateSellRefund(TowerDefinition tower) =>
        new Gold(tower.Cost.Amount * rules.SellRefundPercent / 100);

    public MatchSummary? TryCreateMatchSummary(EconomyPlayerSet players, SimulationTick completedAtTick) =>
        TryCreateMatchSummary(players, completedAtTick, eliminationOrder: null);

    /// <summary>
    /// The end-of-match record, with placements when the caller knows the order seats fell in.
    /// </summary>
    /// <param name="eliminationOrder">
    /// Tick each seat was eliminated, keyed by seat. Null, or missing a seat, leaves that seat's
    /// <see cref="PlayerEconomySummary.Placement"/> at 0 rather than inventing an order.
    /// </param>
    /// <remarks>
    /// The order has to be supplied rather than derived here: this method sees only the final
    /// player set, where every defeated seat looks identical — same zero lives, no record of when
    /// it reached them. That is exactly why the results screen could not rank anyone.
    /// </remarks>
    public MatchSummary? TryCreateMatchSummary(
        EconomyPlayerSet players,
        SimulationTick completedAtTick,
        IReadOnlyDictionary<PlayerId, long>? eliminationOrder)
    {
        var active = players.ActivePlayers;
        if (active.Count != 1)
        {
            return null;
        }

        // Latest elimination first, so the seat that survived longest places second behind the
        // winner. Seats with no recorded tick sort last and keep placement 0.
        var ranked = eliminationOrder is null
            ? new List<PlayerId>()
            : players.Players
                .Where(player => player.IsEliminated && eliminationOrder.ContainsKey(player.PlayerId))
                .OrderByDescending(player => eliminationOrder[player.PlayerId])
                .Select(player => player.PlayerId)
                .ToList();

        var summaries = players.Players
            .Select(player =>
            {
                var placement = player.PlayerId.Equals(active[0].PlayerId)
                    ? 1
                    : ranked.IndexOf(player.PlayerId) is var index && index >= 0 ? index + 2 : 0;
                return new PlayerEconomySummary(
                    player.PlayerId, player.Gold, player.Income, player.Lives, player.IsEliminated, placement);
            })
            .ToArray();

        return new MatchSummary(active[0].PlayerId, completedAtTick, summaries);
    }
}
