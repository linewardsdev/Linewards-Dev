#nullable enable

using System.IO;
using System.Text;
using LTW.Simulation.Events;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalPlaytestRecorder : MonoBehaviour
    {
        private UnitySimulationDriver simulationDriver = null!;
        private LocalReplayExporter replayExporter = null!;
        private long? firstSendTick;
        private long? firstLeakTick;
        private long? firstEliminationTick;
        private bool hasAutoExported;
        private long? autoExportedTick;

        public string? LatestReportPath { get; private set; }

        public void Initialize(UnitySimulationDriver driver, LocalReplayExporter exporter)
        {
            simulationDriver = driver;
            replayExporter = exporter;
        }

        private void Update()
        {
            if (simulationDriver is null)
            {
                return;
            }

            foreach (var simulationEvent in simulationDriver.LatestEvents)
            {
                if (firstSendTick is null && simulationEvent is CreepQueuedEvent)
                {
                    firstSendTick = simulationEvent.Tick.Value;
                }

                if (firstLeakTick is null && simulationEvent is LeakEvent)
                {
                    firstLeakTick = simulationEvent.Tick.Value;
                }

                if (firstEliminationTick is null && simulationEvent is PlayerEliminatedEvent)
                {
                    firstEliminationTick = simulationEvent.Tick.Value;
                }
            }

            var replay = simulationDriver.LatestReplay;
            if (!hasAutoExported && replay is not null && simulationDriver.LatestMatchSummary is not null)
            {
                ExportNow();
                hasAutoExported = true;
                autoExportedTick = replay.CompletedAtTick.Value;
            }
        }

        public string? ExportNow()
        {
            var replay = simulationDriver.LatestReplay;
            if (replay is null)
            {
                return null;
            }

            var replayPath = replayExporter.ExportCurrentReplay();
            var directory = Path.Combine(Application.persistentDataPath, "Playtests");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"playtest-{replay.CompletedAtTick.Value}.md");
            File.WriteAllText(path, BuildReport(replayPath));
            LatestReportPath = path;
            return path;
        }

        public void ResetRecorder()
        {
            firstSendTick = null;
            firstLeakTick = null;
            firstEliminationTick = null;
            hasAutoExported = false;
            autoExportedTick = null;
            LatestReportPath = null;
        }

        private string BuildReport(string? replayPath)
        {
            var replay = simulationDriver.LatestReplay;
            var summary = simulationDriver.LatestMatchSummary;
            var builder = new StringBuilder();
            builder.AppendLine("# Local LTW Playtest Report");
            builder.AppendLine();
            builder.AppendLine($"- Seed: {replay?.Seed ?? 0}");
            builder.AppendLine($"- Content Version: {replay?.ContentVersion ?? "unknown"}");
            builder.AppendLine($"- Map: {replay?.MapId.Value ?? "unknown"}");
            builder.AppendLine($"- Completed Tick: {replay?.CompletedAtTick.Value ?? 0}");
            builder.AppendLine($"- Winner: {(summary is null ? "in-progress" : "P" + summary.WinnerId.Value)}");
            builder.AppendLine($"- First Send Tick: {FormatTick(firstSendTick)}");
            builder.AppendLine($"- First Leak Tick: {FormatTick(firstLeakTick)}");
            builder.AppendLine($"- First Elimination Tick: {FormatTick(firstEliminationTick)}");
            builder.AppendLine($"- Replay Path: {replayPath ?? "not exported"}");

            if (simulationDriver.LatestBotDiagnostics is { } diagnostics)
            {
                builder.AppendLine();
                builder.AppendLine("## Bot Profiles");
                foreach (var profile in diagnostics.Profiles)
                {
                    builder.AppendLine($"- P{profile.PlayerId.Value}: {profile.Profile}, primary send {profile.PrimaryCreepId.Value}");
                }

                builder.AppendLine();
                builder.AppendLine("## Recent Bot Decisions");
                foreach (var decision in diagnostics.RecentDecisions)
                {
                    builder.AppendLine($"- Tick {decision.Tick.Value}: P{decision.PlayerId.Value} sent x{decision.Quantity} {decision.ContentId.Value}");
                }
            }

            if (summary is not null)
            {
                builder.AppendLine();
                builder.AppendLine("## Final Players");
                foreach (var player in summary.Players)
                {
                    builder.AppendLine($"- P{player.PlayerId.Value}: lives {player.Lives.Amount}, income {player.Income.Amount}, gold {player.Gold.Amount}, eliminated {player.IsEliminated}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine("- Fun:");
            builder.AppendLine("- Confusing:");
            builder.AppendLine("- Slow/Broken:");
            builder.AppendLine("- Next Fix:");
            return builder.ToString();
        }

        private static string FormatTick(long? tick) => tick.HasValue ? tick.Value.ToString() : "not observed";
    }
}
