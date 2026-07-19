using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LTW.Simulation.Bots;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Economy;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;

namespace LTW.Simulation.Scenarios;

public sealed class ScenarioRunner
{
    private const int MinPlayerCount = 2;
    private const int MaxPlayerCount = 8;

    private readonly ContentCatalog content;
    private readonly EconomyService economy;
    private readonly CommandContentValidator commandValidator;
    private readonly CreepDefinition sendCreep;
    private readonly ContentId mapId;
    private readonly int playerCount;

    public ScenarioRunner(ContentCatalog content, EconomyService economy, ContentId mapId, ContentId sendCreepId, int playerCount = MaxPlayerCount)
    {
        this.content = content;
        this.economy = economy;
        this.mapId = mapId;
        this.playerCount = Math.Clamp(playerCount, MinPlayerCount, MaxPlayerCount);
        commandValidator = new CommandContentValidator();
        sendCreep = content.Creeps.First(creep => creep.Id.Equals(sendCreepId));
    }

    public ScenarioResult RunThreeBotMatch(int seed, int maxTicks)
    {
        var players = CreatePlayers(playerCount);
        var bots = CreateBots(playerCount);
        var commands = new List<AcceptedCommandRecord>();
        var completedAtTick = new SimulationTick(maxTicks);

        for (var tickValue = 1; tickValue <= maxTicks; tickValue++)
        {
            var tick = new SimulationTick(tickValue);
            players = economy.ApplyIncomeTick(players, tick);

            foreach (var player in players.Players.Where(player => !player.IsEliminated).OrderBy(player => player.PlayerId.Value))
            {
                var decision = bots[player.PlayerId].Decide(player, content, tick);
                if (decision.Command is QueueSendCommand send)
                {
                    players = TryApplySend(players, send, commands);
                }
            }

            players = ApplyScenarioLeakPressure(players, tick);
            if (economy.TryCreateMatchSummary(players, tick) is not null)
            {
                completedAtTick = tick;
                break;
            }
        }

        var replay = new ReplayRecord(seed, content.Version, mapId, players.Players.Select(player => player.PlayerId).ToArray(), completedAtTick, commands);
        return new ScenarioResult(replay, players, HashPlayers(players));
    }

    public ScenarioResult Replay(ReplayRecord replay)
    {
        var players = CreatePlayers(replay.Players.Count);
        var commandsByTick = replay.AcceptedCommands
            .GroupBy(command => command.Tick)
            .ToDictionary(group => group.Key, group => group.ToArray());

        for (var tickValue = 1; tickValue <= replay.CompletedAtTick.Value; tickValue++)
        {
            var tick = new SimulationTick(tickValue);
            players = economy.ApplyIncomeTick(players, tick);
            if (commandsByTick.TryGetValue(tick, out var commands))
            {
                foreach (var command in commands)
                {
                    players = TryApplySend(
                        players,
                        new QueueSendCommand(command.PlayerId, command.Tick, command.ContentId, command.Quantity),
                        acceptedCommands: null);
                }
            }

            players = ApplyScenarioLeakPressure(players, tick);
        }

        return new ScenarioResult(replay, players, HashPlayers(players));
    }

    private EconomyPlayerSet TryApplySend(
        EconomyPlayerSet players,
        QueueSendCommand send,
        List<AcceptedCommandRecord>? acceptedCommands)
    {
        var contentResult = commandValidator.Validate(send, content);
        if (!contentResult.Accepted)
        {
            return players;
        }

        var sendResult = economy.QueueSend(players, send.PlayerId, sendCreep, send.Quantity, send.RequestedTick);
        if (!sendResult.Accepted)
        {
            return players;
        }

        acceptedCommands?.Add(new AcceptedCommandRecord(send.RequestedTick, send.PlayerId, send.CreepId, send.Quantity));
        return sendResult.Players;
    }

    private EconomyPlayerSet ApplyScenarioLeakPressure(EconomyPlayerSet players, SimulationTick tick)
    {
        if (tick.Value % 7 != 0)
        {
            return players;
        }

        var richest = players.Players
            .Where(player => !player.IsEliminated)
            .OrderByDescending(player => player.Income.Amount)
            .ThenBy(player => player.PlayerId.Value)
            .FirstOrDefault();

        if (richest is null)
        {
            return players;
        }

        var target = players.GetCarouselTarget(richest.PlayerId);
        return economy.ApplyLeak(players, richest.PlayerId, target, sendCreep).Players;
    }

    private Dictionary<PlayerId, BotController> CreateBots(int count) =>
        Enumerable.Range(1, count)
            .Select(playerId => new PlayerId(playerId))
            .ToDictionary(
                playerId => playerId,
                playerId => new BotController(BotProfileFor(playerId), sendCreep.Id));

    private static BotDecisionProfile BotProfileFor(PlayerId playerId) =>
        (playerId.Value % 3) switch
        {
            1 => BotDecisionProfile.Greedy,
            2 => BotDecisionProfile.Balanced,
            _ => BotDecisionProfile.Defensive
        };

    private static EconomyPlayerSet CreatePlayers(int count) =>
        new EconomyPlayerSet(Enumerable.Range(1, Math.Clamp(count, MinPlayerCount, MaxPlayerCount))
            .Select(playerId => new PlayerEconomyState(new PlayerId(playerId), new Gold(100), new Income(10), new Lives(5))));

    private static string HashPlayers(EconomyPlayerSet players)
    {
        var input = string.Join(
            "|",
            players.Players
                .OrderBy(player => player.PlayerId.Value)
                .Select(player => $"{player.PlayerId.Value}:{player.Gold.Amount}:{player.Income.Amount}:{player.Lives.Amount}:{player.IsEliminated}:{player.NextSendAvailableTick.Value}"));

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
