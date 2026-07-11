using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Replay;

public sealed class ReplayRecord
{
    public ReplayRecord(
        int seed,
        string contentVersion,
        ContentId mapId,
        IReadOnlyList<PlayerId> players,
        SimulationTick completedAtTick,
        IReadOnlyList<AcceptedCommandRecord> acceptedCommands)
    {
        Seed = seed;
        ContentVersion = string.IsNullOrWhiteSpace(contentVersion)
            ? throw new ArgumentException("Content version is required.", nameof(contentVersion))
            : contentVersion;
        MapId = mapId;
        Players = players.ToArray();
        CompletedAtTick = completedAtTick;
        AcceptedCommands = acceptedCommands.ToArray();
    }

    public int Seed { get; }

    public string ContentVersion { get; }

    public ContentId MapId { get; }

    public IReadOnlyList<PlayerId> Players { get; }

    public SimulationTick CompletedAtTick { get; }

    public IReadOnlyList<AcceptedCommandRecord> AcceptedCommands { get; }
}
