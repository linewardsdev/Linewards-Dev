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

    /// <summary>
    /// Replaces an earlier test named "Repeat_sends_are_allowed_until_gold_runs_out", which built
    /// the service with <c>sendCooldownTicks: 30</c>, sent at ticks 10 and 20, and asserted the
    /// second send was accepted. That described the behavior at the time rather than the intended
    /// rule: the cooldown was configured and its state was tracked, but nothing ever enforced it
    /// (see EconomyService.QueueSend). Now that it is enforced, a resend inside the window must be
    /// rejected, and only gold limits sends once the window has elapsed.
    /// </summary>
    [Fact]
    public void Repeat_sends_are_gated_by_the_send_cooldown_then_by_gold()
    {
        var service = CreateService(sendCooldownTicks: 30);
        var players = CreatePlayers();
        var first = service.QueueSend(players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(10));
        Assert.True(first.Accepted);

        var insideWindow = service.QueueSend(first.Players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(20));

        Assert.False(insideWindow.Accepted);
        Assert.Equal(CommandRejectionReason.CooldownActive, insideWindow.RejectionReason);
        // Rejected sends must not move gold or income.
        Assert.Equal(90, insideWindow.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(11, insideWindow.Players.Get(new PlayerId(1)).Income.Amount);

        // Exactly at first send tick + cooldown the next send is allowed again.
        var afterWindow = service.QueueSend(first.Players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(40));

        Assert.True(afterWindow.Accepted);
        Assert.Equal(80, afterWindow.Players.Get(new PlayerId(1)).Gold.Amount);
        Assert.Equal(12, afterWindow.Players.Get(new PlayerId(1)).Income.Amount);
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
    public void Leak_does_not_credit_gold_to_an_already_eliminated_sender()
    {
        // A sender's creep can still be mid-lane and leak after its owner is eliminated (queued
        // before the elimination). The defender must still lose lives, but the eliminated sender —
        // out of the match — should not gain gold from it, matching ApplyKillBounty's existing
        // refusal to pay out against an eliminated participant.
        var service = CreateService(leakLifeLoss: 2);
        var players = CreatePlayers();
        var eliminatedSender = players.Get(new PlayerId(1)).WithLives(new Lives(0));
        players = players.Replace(eliminatedSender);

        var result = service.ApplyLeak(players, new PlayerId(1), new PlayerId(2), Runner());

        Assert.Equal(2, result.LivesLost.Amount);
        Assert.Equal(18, result.Players.Get(new PlayerId(2)).Lives.Amount);
        Assert.Equal(100, result.Players.Get(new PlayerId(1)).Gold.Amount);
    }

    [Fact]
    public void Explicit_leak_loss_can_exceed_default_rule_for_special_pressure()
    {
        var service = CreateService(leakLifeLoss: 1);
        var players = CreatePlayers();

        var result = service.ApplyLeak(players, new PlayerId(1), new PlayerId(2), Runner(), new Lives(2));

        Assert.Equal(2, result.LivesLost.Amount);
        Assert.Equal(18, result.Players.Get(new PlayerId(2)).Lives.Amount);
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

    /// <summary>
    /// Category 2 creeps send whenever the player can afford them.
    /// </summary>
    [Fact]
    public void Exempt_creeps_ignore_the_send_cooldown()
    {
        var service = CreateService(sendCooldownTicks: 30);
        var players = CreatePlayers();

        var first = service.QueueSend(players, new PlayerId(1), ExemptCreep(), quantity: 1, new SimulationTick(10));
        Assert.True(first.Accepted);

        var immediatelyAgain = service.QueueSend(first.Players, new PlayerId(1), ExemptCreep(), quantity: 1, new SimulationTick(11));

        Assert.True(immediatelyAgain.Accepted);
    }

    /// <summary>
    /// An exempt send must not arm the cooldown, or spamming the cheapest Category 2 creep would
    /// lock out every Category 1 send.
    /// </summary>
    [Fact]
    public void Exempt_sends_do_not_arm_the_cooldown_for_other_creeps()
    {
        var service = CreateService(sendCooldownTicks: 30);
        var players = CreatePlayers();

        var exempt = service.QueueSend(players, new PlayerId(1), ExemptCreep(), quantity: 1, new SimulationTick(10));
        Assert.True(exempt.Accepted);

        var gated = service.QueueSend(exempt.Players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(11));

        Assert.True(gated.Accepted);
    }

    /// <summary>
    /// The exemption must not leak into the normal path: a non-exempt creep is still gated.
    /// </summary>
    [Fact]
    public void Exempt_creeps_do_not_bypass_the_cooldown_for_non_exempt_creeps()
    {
        var service = CreateService(sendCooldownTicks: 30);
        var players = CreatePlayers();

        var first = service.QueueSend(players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(10));
        Assert.True(first.Accepted);

        var exemptStillFine = service.QueueSend(first.Players, new PlayerId(1), ExemptCreep(), quantity: 1, new SimulationTick(11));
        var runnerStillGated = service.QueueSend(first.Players, new PlayerId(1), Runner(), quantity: 1, new SimulationTick(11));

        Assert.True(exemptStillFine.Accepted);
        Assert.False(runnerStillGated.Accepted);
        Assert.Equal(CommandRejectionReason.CooldownActive, runnerStillGated.RejectionReason);
    }

    /// <summary>
    /// Gold is still the gate for exempt creeps — "send at any time" means no timer, not free.
    /// </summary>
    [Fact]
    public void Exempt_creeps_are_still_gated_by_gold()
    {
        var service = CreateService(sendCooldownTicks: 30);
        var players = CreatePlayers(gold: 4);

        var result = service.QueueSend(players, new PlayerId(1), ExemptCreep(), quantity: 1, new SimulationTick(10));

        Assert.False(result.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, result.RejectionReason);
    }

    /// <summary>
    /// A batched send costs unit price times quantity, and is rejected on the total.
    /// </summary>
    /// <remarks>
    /// Pins the rule the send dock got wrong. Swarm is sent three at a time and costs 6 gold each,
    /// so a press costs 18. The HUD priced the card at the unit cost, enabled it whenever the
    /// player could afford one, and then reported the unit cost back in the rejection — producing
    /// "Need 6G (have 15G)", which is self-contradictory on its face because the two numbers came
    /// from different calculations.
    ///
    /// Gold here is 15 deliberately: enough for two of the three, so a check against the unit cost
    /// passes and a check against the total fails. A test at 5 gold would pass either way and would
    /// not have caught this.
    /// </remarks>
    [Fact]
    public void Batched_send_is_priced_and_rejected_on_the_total_not_the_unit_cost()
    {
        var service = CreateService();
        var swarm = Swarm();

        var affordable = service.QueueSend(CreatePlayers(gold: 18), new PlayerId(1), swarm, quantity: 3, new SimulationTick(10));
        Assert.True(affordable.Accepted);
        Assert.Equal(0, affordable.Players.Get(new PlayerId(1)).Gold.Amount);

        var shortOfTheTotal = service.QueueSend(CreatePlayers(gold: 15), new PlayerId(1), swarm, quantity: 3, new SimulationTick(10));
        Assert.False(shortOfTheTotal.Accepted);
        Assert.Equal(CommandRejectionReason.InsufficientGold, shortOfTheTotal.RejectionReason);
        Assert.Equal(15, shortOfTheTotal.Players.Get(new PlayerId(1)).Gold.Amount);
    }

    private static CreepDefinition Runner() =>
        new(new ContentId("creep.runner"), "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 15, speedPerSecond: 2, categoryIndex: 0);

    /// <summary>Matches the shipped Swarm: the only creep sent in a batch.</summary>
    private static CreepDefinition Swarm() =>
        new(new ContentId("creep.swarm"), "Swarm", new Gold(6), new Income(1), new Gold(1), new Gold(1), maxHealth: 5, speedPerSecond: 2, categoryIndex: 0);

    private static CreepDefinition ExemptCreep() =>
        new(new ContentId("creep.wisp"), "Crystal Wisp", new Gold(5), new Income(1), new Gold(1), new Gold(1), maxHealth: 4, speedPerSecond: 3, categoryIndex: 0, ignoresSendCooldown: true);

    private static TowerDefinition ArrowTower() =>
        new(new ContentId("tower.arrow"), "Arrow Tower", new Gold(25), rangeCells: 3, damage: 5, attackCooldownTicks: 10, categoryIndex: 0);
}
