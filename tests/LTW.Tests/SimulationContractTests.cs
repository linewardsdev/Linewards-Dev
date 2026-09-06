using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using LTW.Simulation.Random;
using LTW.Simulation.State;

namespace LTW.Tests;

public sealed class SimulationContractTests
{
    [Fact]
    public void Valid_sample_content_passes_validation()
    {
        var content = SampleContent.Valid();

        var result = new ContentValidator().Validate(content);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Malformed_content_reports_duplicate_missing_reference_and_map_errors()
    {
        var content = SampleContent.Malformed();

        var result = new ContentValidator().Validate(content);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Duplicate tower ID", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("positive cost", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("references missing creep", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("spawn cannot be blocked", StringComparison.Ordinal));
    }

    [Fact]
    public void Commands_can_be_created_and_validated_without_unity()
    {
        var content = SampleContent.Valid();
        var validator = new CommandContentValidator();
        var command = new PlaceTowerCommand(
            new PlayerId(1),
            new SimulationTick(12),
            new LaneId(1),
            SampleContent.ArrowTowerId,
            new GridPosition(2, 3));

        var result = validator.Validate(command, content);

        Assert.True(result.Accepted);
        Assert.Equal(CommandRejectionReason.None, result.RejectionReason);
    }

    [Fact]
    public void Command_validation_rejects_unknown_content_and_invalid_quantities()
    {
        var content = SampleContent.Valid();
        var validator = new CommandContentValidator();

        var unknownTower = validator.Validate(
            new PlaceTowerCommand(new PlayerId(1), new SimulationTick(0), new LaneId(1), new ContentId("missing"), new GridPosition(0, 0)),
            content);
        var invalidSend = validator.Validate(
            new QueueSendCommand(new PlayerId(1), new SimulationTick(0), SampleContent.RunnerCreepId, 0),
            content);
        var unknownTech = validator.Validate(
            new BuyTechCommand(new PlayerId(1), new SimulationTick(0), new ContentId("missing-tech")),
            content);

        Assert.Equal(CommandRejectionReason.UnknownTower, unknownTower.RejectionReason);
        Assert.Equal(CommandRejectionReason.InvalidQuantity, invalidSend.RejectionReason);
        Assert.Equal(CommandRejectionReason.UnknownTech, unknownTech.RejectionReason);
    }

    /// <summary>
    /// docs/SECURITY_AUDIT_2026-09-05.md's C1: an unbounded quantity reaches
    /// EconomyService.SendCostFor's unchecked int multiply and a spawn loop sized off the same
    /// value — a large-enough quantity overflows the cost to something small (or negative,
    /// crediting gold) and/or exhausts memory trying to spawn that many creeps. This must be
    /// rejected here, before either of those, not merely bounded below zero.
    /// </summary>
    [Fact]
    public void Command_validation_rejects_a_queue_send_quantity_above_the_queue_depth_cap()
    {
        var content = SampleContent.Valid();
        var validator = new CommandContentValidator();

        var withinBounds = validator.Validate(
            new QueueSendCommand(new PlayerId(1), new SimulationTick(0), SampleContent.RunnerCreepId, LTW.Simulation.Bridge.LocalVerticalSlice.MaxQueuedSendsPerCreep),
            content);
        var overflowAttempt = validator.Validate(
            new QueueSendCommand(new PlayerId(1), new SimulationTick(0), SampleContent.RunnerCreepId, 429_496_730),
            content);
        var justOverTheCap = validator.Validate(
            new QueueSendCommand(new PlayerId(1), new SimulationTick(0), SampleContent.RunnerCreepId, LTW.Simulation.Bridge.LocalVerticalSlice.MaxQueuedSendsPerCreep + 1),
            content);

        Assert.Equal(CommandRejectionReason.None, withinBounds.RejectionReason);
        Assert.Equal(CommandRejectionReason.InvalidQuantity, overflowAttempt.RejectionReason);
        Assert.Equal(CommandRejectionReason.InvalidQuantity, justOverTheCap.RejectionReason);
    }

    [Fact]
    public void Command_validation_rejects_default_identifiers()
    {
        var content = SampleContent.Valid();
        var validator = new CommandContentValidator();

        var invalidPlayer = validator.Validate(
            new QueueSendCommand(default, new SimulationTick(0), SampleContent.RunnerCreepId, 1),
            content);
        var invalidLane = validator.Validate(
            new PlaceTowerCommand(new PlayerId(1), new SimulationTick(0), default, SampleContent.ArrowTowerId, new GridPosition(0, 0)),
            content);
        var invalidEntity = validator.Validate(
            new SellTowerCommand(new PlayerId(1), new SimulationTick(0), default),
            content);
        var invalidContent = validator.Validate(
            new QueueSendCommand(new PlayerId(1), new SimulationTick(0), default, 1),
            content);

        Assert.Equal(CommandRejectionReason.InvalidPlayer, invalidPlayer.RejectionReason);
        Assert.Equal(CommandRejectionReason.InvalidLane, invalidLane.RejectionReason);
        Assert.Equal(CommandRejectionReason.InvalidEntity, invalidEntity.RejectionReason);
        Assert.Equal(CommandRejectionReason.InvalidContentId, invalidContent.RejectionReason);
    }

    [Fact]
    public void Content_catalog_defensively_copies_source_collections()
    {
        var towers = new List<TowerDefinition>
        {
            new(SampleContent.ArrowTowerId, "Arrow Tower", new Gold(25), rangeCells: 3, damage: 5, attackCooldownTicks: 10, categoryIndex: 0)
        };

        var content = new ContentCatalog(
            "copy-test",
            towers,
            Array.Empty<CreepDefinition>(),
            Array.Empty<TechDefinition>(),
            Array.Empty<MapDefinition>(),
            Array.Empty<BotProfileDefinition>());

        towers.Clear();

        Assert.Single(content.Towers);
    }

    [Fact]
    public void Match_snapshot_defensively_copies_source_collections()
    {
        var players = new List<PlayerSnapshot>
        {
            new(new PlayerId(1), new Gold(50), new Income(10), new Lives(20), isEliminated: false)
        };

        var snapshot = new MatchSnapshot(new SimulationTick(0), "copy-test", players, Array.Empty<LaneSnapshot>());

        players.Clear();

        Assert.Single(snapshot.Players);
    }

    [Fact]
    public void Seeded_random_source_repeats_for_the_same_seed()
    {
        var first = new SeededRandomSource(8675309);
        var second = new SeededRandomSource(8675309);

        Assert.Equal(first.NextInt(0, 100), second.NextInt(0, 100));
        Assert.Equal(first.NextDouble(), second.NextDouble());
    }
}

internal static class SampleContent
{
    public static readonly ContentId ArrowTowerId = new("tower.arrow");

    public static readonly ContentId RunnerCreepId = new("creep.runner");

    private static readonly ContentId BasicTechId = new("tech.basic");

    private static readonly ContentId TestMapId = new("map.test");

    private static readonly ContentId BalancedBotId = new("bot.balanced");

    public static ContentCatalog Valid()
    {
        return new ContentCatalog(
            "mvp-01-test",
            new[]
            {
                new TowerDefinition(ArrowTowerId, "Arrow Tower", new Gold(25), rangeCells: 3, damage: 5, attackCooldownTicks: 10, categoryIndex: 0)
            },
            new[]
            {
                new CreepDefinition(RunnerCreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 15, speedPerSecond: 2, categoryIndex: 0)
            },
            new[]
            {
                new TechDefinition(BasicTechId, "Basic Tech", new Gold(50), new[] { ArrowTowerId }, new[] { RunnerCreepId })
            },
            new[]
            {
                new MapDefinition(TestMapId, "Test Lane", width: 8, height: 6, new GridPosition(0, 2), new GridPosition(7, 2), Array.Empty<GridPosition>())
            },
            new[]
            {
                new BotProfileDefinition(BalancedBotId, "Balanced", aggression: 5, defenseBias: 5, minimumGoldReserve: 10)
            });
    }

    public static ContentCatalog Malformed()
    {
        return new ContentCatalog(
            "mvp-01-bad",
            new[]
            {
                new TowerDefinition(ArrowTowerId, "Arrow Tower", new Gold(25), rangeCells: 3, damage: 5, attackCooldownTicks: 10, categoryIndex: 0),
                new TowerDefinition(ArrowTowerId, "Duplicate Arrow Tower", new Gold(0), rangeCells: 3, damage: 5, attackCooldownTicks: 10, categoryIndex: 0)
            },
            new[]
            {
                new CreepDefinition(RunnerCreepId, "Runner", new Gold(10), new Income(1), new Gold(1), new Gold(2), maxHealth: 15, speedPerSecond: 2, categoryIndex: 0)
            },
            new[]
            {
                new TechDefinition(BasicTechId, "Broken Tech", new Gold(50), new[] { ArrowTowerId }, new[] { new ContentId("creep.missing") })
            },
            new[]
            {
                new MapDefinition(TestMapId, "Broken Lane", width: 8, height: 6, new GridPosition(0, 2), new GridPosition(7, 2), new[] { new GridPosition(0, 2) })
            },
            new[]
            {
                new BotProfileDefinition(BalancedBotId, "Balanced", aggression: 5, defenseBias: 5, minimumGoldReserve: 10)
            });
    }
}
