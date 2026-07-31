using System;
using System.Collections.Generic;
using System.Linq;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;

namespace LTW.Simulation.Replay;

/// <summary>
/// Send-only telemetry for a completed match: who sent what, when. Not a reproducible replay.
/// </summary>
/// <remarks>
/// <see cref="AcceptedCommands"/> only ever grows from <c>QueueSend</c> — <c>PlaceTower</c> and
/// <c>SellTower</c> are never recorded, and <see cref="AcceptedCommandRecord"/> carries no fields
/// (command kind, lane, position) that could hold them even if they were. Since tower placement
/// determines the route and every kill, this record cannot reproduce a
/// <c>LocalVerticalSlice</c> match, and no code path attempts to: <c>ScenarioRunner.Replay</c> runs
/// a separate economy-only model against send data, not this record.
///
/// <see cref="Seed"/> is recorded but consumed nowhere — the simulation is all-integer and
/// deterministic already, so nothing needs re-seeding. It documents what seed a match was
/// configured with, not a guarantee that record can reconstruct it.
///
/// This is deliberate scope, not an oversight: full match replay (recording every accepted
/// command with enough detail to rebuild the match) would be a real feature, not a bug fix. Treat
/// this type as send/economy telemetry for tuning and evidence, and see OPEN_ITEMS.md's retired 2026-07-29 review, "replay records cannot reproduce a match" for
/// the reasoning if that scope ever needs revisiting.
/// </remarks>
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

    /// <summary>Decorative today — see the class remarks. Nothing re-seeds from this.</summary>
    public int Seed { get; }

    public string ContentVersion { get; }

    public ContentId MapId { get; }

    public IReadOnlyList<PlayerId> Players { get; }

    public SimulationTick CompletedAtTick { get; }

    public IReadOnlyList<AcceptedCommandRecord> AcceptedCommands { get; }
}
