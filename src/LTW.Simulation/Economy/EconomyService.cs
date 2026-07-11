using System;
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
        if (quantity <= 0)
        {
            return SendResult.Reject(players, CommandRejectionReason.InvalidQuantity);
        }

        var sender = players.Get(senderId);
        if (sender.IsEliminated)
        {
            return SendResult.Reject(players, CommandRejectionReason.PlayerEliminated);
        }

        if (requestedTick.CompareTo(sender.NextSendAvailableTick) < 0)
        {
            return SendResult.Reject(players, CommandRejectionReason.CooldownActive);
        }

        var cost = creep.Cost.Amount * quantity;
        if (sender.Gold.Amount < cost)
        {
            return SendResult.Reject(players, CommandRejectionReason.InsufficientGold);
        }

        var updatedSender = sender
            .WithGold(new Gold(sender.Gold.Amount - cost))
            .WithIncome(new Income(sender.Income.Amount + creep.IncomeGain.Amount * quantity))
            .WithNextSendAvailableTick(new SimulationTick(requestedTick.Value + rules.SendCooldownTicks));

        var targetId = players.GetCarouselTarget(senderId);
        return SendResult.Accept(players.Replace(updatedSender), targetId);
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
        var sender = players.Get(senderId);
        var defender = players.Get(defenderId);
        var livesLost = Math.Min(defender.Lives.Amount, rules.LeakLifeLoss);
        var updatedDefender = defender.WithLives(new Lives(defender.Lives.Amount - livesLost));
        var updatedSender = sender.WithGold(new Gold(sender.Gold.Amount + creep.LeakBounty.Amount));

        var next = players.Replace(updatedDefender).Replace(updatedSender);
        return new LeakResult(next, new Lives(livesLost), creep.LeakBounty);
    }

    public Gold CalculateSellRefund(TowerDefinition tower) =>
        new Gold(tower.Cost.Amount * rules.SellRefundPercent / 100);

    public MatchSummary? TryCreateMatchSummary(EconomyPlayerSet players, SimulationTick completedAtTick)
    {
        var active = players.ActivePlayers;
        if (active.Count != 1)
        {
            return null;
        }

        var summaries = players.Players
            .Select(player => new PlayerEconomySummary(player.PlayerId, player.Gold, player.Income, player.Lives, player.IsEliminated))
            .ToArray();

        return new MatchSummary(active[0].PlayerId, completedAtTick, summaries);
    }
}
