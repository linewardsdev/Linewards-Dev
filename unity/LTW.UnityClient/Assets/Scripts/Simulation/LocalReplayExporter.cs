using System.IO;
using System.Text;
using LTW.Simulation.Replay;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalReplayExporter : MonoBehaviour
    {
        [SerializeField] private UnitySimulationDriver simulationDriver = null!;

        public void Initialize(UnitySimulationDriver driver) => simulationDriver = driver;

        public string? ExportCurrentReplay()
        {
            var replay = simulationDriver.LatestReplay;
            if (replay == null) return null;

            var directory = Path.Combine(Application.persistentDataPath, "Replays");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"match-{replay.CompletedAtTick.Value}.json");
            File.WriteAllText(path, Serialize(replay));
            return path;
        }

        private static string Serialize(ReplayRecord replay)
        {
            var builder = new StringBuilder();
            builder.Append("{\"seed\":").Append(replay.Seed)
                .Append(",\"contentVersion\":\"").Append(replay.ContentVersion).Append("\"")
                .Append(",\"mapId\":\"").Append(replay.MapId.Value).Append("\"")
                .Append(",\"completedAtTick\":").Append(replay.CompletedAtTick.Value)
                .Append(",\"commands\":[");
            for (var index = 0; index < replay.AcceptedCommands.Count; index++)
            {
                var command = replay.AcceptedCommands[index];
                if (index > 0) builder.Append(',');
                builder.Append("{\"tick\":").Append(command.Tick.Value)
                    .Append(",\"playerId\":").Append(command.PlayerId.Value)
                    .Append(",\"contentId\":\"").Append(command.ContentId.Value).Append("\"")
                    .Append(",\"quantity\":").Append(command.Quantity).Append('}');
            }

            return builder.Append("]}").ToString();
        }
    }
}
