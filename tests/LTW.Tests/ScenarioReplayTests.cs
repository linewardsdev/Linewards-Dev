using LTW.Simulation.Bots;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.Simulation.Scenarios;

namespace LTW.Tests;

public sealed class ScenarioReplayTests
{
    [Fact]
    public void Three_bots_complete_match_without_invalid_state_or_exceptions()
    {
        var runner = CreateRunner();

        var result = runner.RunThreeBotMatch(seed: 1234, maxTicks: 400);

        Assert.NotEmpty(result.Replay.AcceptedCommands);
        Assert.Equal(8, result.Replay.Players.Count);
        Assert.Contains(result.Replay.AcceptedCommands, command => command.PlayerId.Equals(new PlayerId(8)));
        Assert.Single(result.Players.ActivePlayers);
        Assert.All(result.Players.Players, player =>
        {
            Assert.True(player.Gold.Amount >= 0);
            Assert.True(player.Income.Amount >= 0);
            Assert.True(player.Lives.Amount >= 0);
        });
    }

    [Fact]
    public void Replaying_saved_match_produces_same_final_state_hash()
    {
        var runner = CreateRunner();
        var result = runner.RunThreeBotMatch(seed: 5678, maxTicks: 80);

        var replayed = runner.Replay(result.Replay);

        Assert.Equal(result.FinalStateHash, replayed.FinalStateHash);
    }

    [Fact]
    public void Bot_profiles_produce_different_income_versus_defense_behavior()
    {
        var content = CreateContent();
        var creepId = RunnerCreepId;
        var greedy = new BotController(BotDecisionProfile.Greedy, creepId);
        var balanced = new BotController(BotDecisionProfile.Balanced, creepId);
        var defensive = new BotController(BotDecisionProfile.Defensive, creepId);
        var state = new PlayerEconomyState(new PlayerId(1), new Gold(100), new Income(10), new Lives(5));

        var greedySend = Assert.IsType<LTW.Simulation.Commands.QueueSendCommand>(greedy.Decide(state, content, new SimulationTick(1)).Command);
        var balancedSend = Assert.IsType<LTW.Simulation.Commands.QueueSendCommand>(balanced.Decide(state, content, new SimulationTick(1)).Command);
        var defensiveSend = Assert.IsType<LTW.Simulation.Commands.QueueSendCommand>(defensive.Decide(state, content, new SimulationTick(1)).Command);

        Assert.True(greedySend.Quantity > balancedSend.Quantity);
        Assert.True(balancedSend.Quantity > defensiveSend.Quantity);
    }

    [Fact]
    public void Stress_scenario_records_heavy_sends_and_replay_commands()
    {
        var runner = CreateRunner();

        var result = runner.RunThreeBotMatch(seed: 999, maxTicks: 120);

        Assert.True(result.Replay.AcceptedCommands.Count >= 10);
        Assert.Contains(result.Replay.AcceptedCommands, command => command.Quantity > 1);
    }

    private static ScenarioRunner CreateRunner()
    {
        return new ScenarioRunner(
            CreateContent(),
            new EconomyService(new EconomyRules(incomeIntervalTicks: 5, sendCooldownTicks: 3, sellRefundPercent: 50, leakLifeLoss: 1)),
            TestMapId,
            RunnerCreepId);
    }

    private static readonly ContentId RunnerCreepId = new("creep.runner");

    private static readonly ContentId ArrowTowerId = new("tower.arrow");

    private static readonly ContentId TestMapId = new("map.test");

    private static ContentCatalog CreateContent() =>
        new(
            "scenario-test",
            new[] { new TowerDefinition(ArrowTowerId, "Arrow Tower", new Gold(25), rangeCells: 3, damage: 5, attackCooldownTicks: 10) },
            new[] { new CreepDefinition(RunnerCreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 15, speedPerSecond: 2) },
            Array.Empty<TechDefinition>(),
            new[] { new MapDefinition(TestMapId, "Test", width: 8, height: 6, new GridPosition(0, 3), new GridPosition(7, 3), Array.Empty<GridPosition>()) },
            new[]
            {
                new BotProfileDefinition(new ContentId("bot.greedy"), "Greedy", aggression: 10, defenseBias: 0, minimumGoldReserve: 0),
                new BotProfileDefinition(new ContentId("bot.balanced"), "Balanced", aggression: 5, defenseBias: 5, minimumGoldReserve: 20),
                new BotProfileDefinition(new ContentId("bot.defensive"), "Defensive", aggression: 1, defenseBias: 10, minimumGoldReserve: 70)
            });
}
