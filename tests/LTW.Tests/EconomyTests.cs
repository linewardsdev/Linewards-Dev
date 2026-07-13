using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;

namespace LTW.Tests;

public sealed class EconomyTests
{
    [Fact]
    public void Send_targets_next_carousel_player_and_changes_income_once()
    {
        var service = CreateService();
        var players = CreatePlayers();

        var result = service.QueueSend(players, new PlayerId(1), Runner(), quantity: 2, new SimulationTick(10));

        Assert.True(result.Accepted);
        Assert.Equal(new PlayerId(2), result.TargetPlayerId);

        var sender = result.Players.Get(new PlayerId(1));
        Assert.Equal(80, sender.Gold.Amount);
        Assert.Equal(12, sender.Income.Amount);
    }

    [Fact]
    public void Insufficient_gold_rejects_without_changing_state()
    {
        var service = CreateService();
        var players = CreatePlayers(gold: 5);

        var result = service.QueueSend(players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(10));

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, result.RejectionReason);
        Assert.Equal(5, result.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(10, result.Players.Get(new PlayerId(1)).Income.Amount);
    }

    [Fact]
    public void Repeat_sends_are_allowed_until_gold_runs_out()
    {
        var service = CreateService(sendCooldownTicks: 30);
        var players = CreatePlayers();
        var first = service.QueueSend(players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(10));

        var second = service.QueueSend(first.Players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(20));

        Assert.True(second.Accepted);
        Assert.Equal(80, second.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(12, second.Players.Get(new PlayerId(1)).Income.Amount);
    }

    [Fact]
    public void Leak_affects_defender_and_credits_sender()
    {
        var service = CreateService(leakLifeLoss: 2);
        var players = CreatePlayers();

        var result = service.ApplyLeak(players, new PlayerId(1), new PlayerId(2), Runner());

        Assert.Equal(2, result.LivesLost.Amount);
        Assert.Equal(18, result.Players.Get(new PlayerId(2)).Lives.Amount);
        Assert.Equal(102, result.Players.Get(new PlayerId(1)).Gold.Amount);
    }

    [Fact]
    public void Completed_elimination_sequence_produces_one_winner()
    {
        var service = CreateService(leakLifeLoss: 20);
        var players = CreatePlayers();

        players = service.ApplyLeak(players, new PlayerId(1), new PlayerId(2), Runner()).Players;
        players = service.ApplyLeak(players, new PlayerId(1), new PlayerId(3), Runner()).Players;

        var summary = service.TryCreateMatchSummary(players, new SimulationTick(100));

        Assert.NotNull(summary);
        Assert.Equal(new PlayerId(1), summary.WinnerId);
        Assert.Equal(new SimulationTick(100), summary.CompletedAtTick);
        Assert.Equal(3, summary.Players.Count);
    }

    [Fact]
    public void Income_tick_awards_income_on_schedule_only()
    {
        var service = CreateService(incomeIntervalTicks: 25);
        var players = CreatePlayers();

        var unchanged = service.ApplyIncomeTick(players, new SimulationTick(24));
        var changed = service.ApplyIncomeTick(players, new SimulationTick(25));

        Assert.Equal(100, unchanged.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(110, changed.Get(new PlayerId(1)).Gold.Amount);
    }

    [Fact]
    public void Kill_bounty_and_sell_refund_follow_configured_rules()
    {
        var service = CreateService(sellRefundPercent: 75);
        var players = CreatePlayers();

        var bounty = service.ApplyKillBounty(players, new PlayerId(2), Runner());
        var refund = service.CalculateSellRefund(ArrowTower());

        Assert.True(bounty.Accepted);
        Assert.Equal(101, bounty.Players.Get(new PlayerId(2)).Gold.Amount);
        Assert.Equal(18, refund.Amount);
    }

    private static EconomyService CreateService(
        int incomeIntervalTicks = 50,
        int sendCooldownTicks = 10,
        int sellRefundPercent = 50,
        int leakLifeLoss = 1)
    {
        return new EconomyService(new EconomyRules(incomeIntervalTicks, sendCooldownTicks, sellRefundPercent, leakLifeLoss));
    }

    private static EconomyPlayerSet CreatePlayers(int gold = 100)
    {
        return new EconomyPlayerSet(new[]
        {
            new PlayerEconomyState(new PlayerId(1), new Gold(gold), new Income(10), new Lives(20)),
            new PlayerEconomyState(new PlayerId(2), new Gold(100), new Income(10), new Lives(20)),
            new PlayerEconomyState(new PlayerId(3), new Gold(100), new Income(10), new Lives(20))
        });
    }

    private static CreepDefinition Runner() =>
        new(new ContentId("creep.runner"), "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 15, speedPerSecond: 2);

    private static TowerDefinition ArrowTower() =>
        new(new ContentId("tower.arrow"), "Arrow Tower", new Gold(25), rangeCells: 3, damage: 5, attackCooldownTicks: 10);
}
