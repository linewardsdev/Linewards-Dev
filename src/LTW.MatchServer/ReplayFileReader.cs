using System.Text.Json;
using LTW.Simulation.Commands;
using LTW.Simulation.Content;
using LTW.Simulation.Primitives;
using LTW.Simulation.Replay;

namespace LTW.MatchServer;

/// <summary>
/// Reads a replay file back into a <see cref="MatchReplayRecord"/> — the other half of
/// <see cref="ServerMatch"/>'s own private <c>WriteReplayAsync</c>, which had no reader anywhere
/// in this codebase before this. Without this, MP07_RUNBOOK.md's own "investigate a desync from
/// its replay" step had no actual mechanism: a written replay file could only ever be read by eye,
/// never fed back into <c>LocalVerticalSlice.Replay</c> to reproduce the match. See
/// docs/SECURITY_AUDIT_2026-09-05.md's M-T1.
/// </summary>
/// <remarks>
/// Deliberately its own DTO shape, matched field-for-field to <c>WriteReplayAsync</c>'s anonymous
/// object, rather than deserializing straight into <see cref="MatchReplayRecord"/>/
/// <see cref="RecordedCommand"/>: both are constructor-validated (<see cref="RecordedCommand"/>
/// has no public constructor at all, only its per-kind factory methods), so nothing here can
/// silently deserialize into an invalid record the way a public-setter DTO could.
/// </remarks>
public static class ReplayFileReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<MatchReplayRecord> ReadAsync(string path)
    {
        var text = await File.ReadAllTextAsync(path);
        return Read(text);
    }

    public static MatchReplayRecord Read(string json)
    {
        var dto = JsonSerializer.Deserialize<ReplayFileDto>(json, Json)
            ?? throw new InvalidDataException("Replay file was empty or not a JSON object.");

        var players = dto.Players.Select(value => new PlayerId(value)).ToArray();
        var commands = dto.Commands.Select(ToRecordedCommand).ToArray();

        return new MatchReplayRecord(
            dto.Seed,
            dto.ContentVersion,
            new ContentId(dto.MapId),
            players,
            new SimulationTick(dto.CompletedAtTick),
            commands);
    }

    private static RecordedCommand ToRecordedCommand(ReplayFileCommandDto command)
    {
        var tick = new SimulationTick(command.Tick);
        var playerId = new PlayerId(command.PlayerId);
        var kind = Enum.Parse<RecordedCommandKind>(command.Kind);

        return kind switch
        {
            RecordedCommandKind.PlaceTower => RecordedCommand.ForPlaceTower(
                tick,
                command.Sequence,
                playerId,
                new LaneId(Require(command.LaneId, command, "laneId")),
                new ContentId(Require(command.ContentId, command, "contentId")),
                new GridPosition(Require(command.X, command, "x"), Require(command.Y, command, "y"))),
            RecordedCommandKind.SellTower => RecordedCommand.ForSellTower(
                tick,
                command.Sequence,
                playerId,
                new LaneId(Require(command.LaneId, command, "laneId")),
                new GridPosition(Require(command.X, command, "x"), Require(command.Y, command, "y"))),
            RecordedCommandKind.UpgradeTower => RecordedCommand.ForUpgradeTower(
                tick,
                command.Sequence,
                playerId,
                new LaneId(Require(command.LaneId, command, "laneId")),
                new GridPosition(Require(command.X, command, "x"), Require(command.Y, command, "y"))),
            RecordedCommandKind.BuyCategoryTier => RecordedCommand.ForBuyCategoryTier(
                tick,
                command.Sequence,
                playerId,
                Enum.Parse<CategoryKind>(Require(command.Category, command, "category")),
                Require(command.CategoryIndex, command, "categoryIndex"),
                Require(command.TargetTier, command, "targetTier")),
            RecordedCommandKind.Send => RecordedCommand.ForSend(
                tick,
                command.Sequence,
                playerId,
                new ContentId(Require(command.ContentId, command, "contentId")),
                Require(command.Quantity, command, "quantity")),
            _ => throw new NotSupportedException($"Unknown recorded command kind '{command.Kind}'."),
        };
    }

    private static T Require<T>(T? value, ReplayFileCommandDto command, string field)
        where T : struct =>
        value ?? throw new InvalidDataException($"Replay command (sequence {command.Sequence}, kind {command.Kind}) is missing '{field}'.");

    private static string Require(string? value, ReplayFileCommandDto command, string field) =>
        value ?? throw new InvalidDataException($"Replay command (sequence {command.Sequence}, kind {command.Kind}) is missing '{field}'.");

    private sealed class ReplayFileDto
    {
        public int Seed { get; set; }

        public string ContentVersion { get; set; } = "";

        public string MapId { get; set; } = "";

        public int[] Players { get; set; } = Array.Empty<int>();

        public long CompletedAtTick { get; set; }

        public ReplayFileCommandDto[] Commands { get; set; } = Array.Empty<ReplayFileCommandDto>();
    }

    private sealed class ReplayFileCommandDto
    {
        public long Tick { get; set; }

        public int Sequence { get; set; }

        public int PlayerId { get; set; }

        public string Kind { get; set; } = "";

        public int? LaneId { get; set; }

        public string? ContentId { get; set; }

        public int? X { get; set; }

        public int? Y { get; set; }

        public string? Category { get; set; }

        public int? CategoryIndex { get; set; }

        public int? TargetTier { get; set; }

        public int? Quantity { get; set; }
    }
}
