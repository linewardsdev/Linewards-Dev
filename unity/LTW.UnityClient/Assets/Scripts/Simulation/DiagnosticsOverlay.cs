#nullable enable

using System.Linq;
using System.Text;
using LTW.Simulation.Primitives;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        /// <summary>
        /// Whether the developer diagnostics panel is drawn over the match.
        /// </summary>
        /// <remarks>
        /// Defaults OFF. It previously defaulted on, so a normal play session showed tick counts,
        /// per-player state, lane flow and bot sends over the top-left of the board — useful while
        /// developing, but it is not something a player should see, and nothing gated it. Opt in
        /// with -ltwDiagnostics, matching the existing -ltwBoardDetail / -ltwCameraTilt switches.
        /// LatestText is still produced either way, since the playtest recorder consumes it.
        /// </remarks>
        [SerializeField]
        private bool showRuntimeOverlay;

        private bool overlayFlagResolved;

        private bool ShouldDrawOverlay()
        {
            if (!overlayFlagResolved)
            {
                overlayFlagResolved = true;
                var args = System.Environment.GetCommandLineArgs();
                for (var index = 0; index < args.Length; index++)
                {
                    if (string.Equals(args[index], "-ltwDiagnostics", System.StringComparison.Ordinal))
                    {
                        showRuntimeOverlay = true;
                        break;
                    }
                }
            }

            return showRuntimeOverlay;
        }

        private GUIStyle? overlayStyle;

        public string LatestText { get; private set; } = string.Empty;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
        }

        private void Update()
        {
            if (simulationDriver == null)
            {
                simulationDriver = Object.FindAnyObjectByType<UnitySimulationDriver>();
                if (simulationDriver == null)
                {
                    return;
                }
            }

            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append("Tick: ").Append(snapshot.Tick.Value);
            builder.Append("  Towers: ").Append(snapshot.Towers.Count)
                .Append("  Creeps: ").Append(snapshot.Creeps.Count);

            builder.Append("\nLane activity:");
            foreach (var player in snapshot.Players.Players)
            {
                var playerId = player.PlayerId;
                var towerCount = snapshot.Towers.Count(tower => tower.OwnerId.Equals(playerId));
                var sentCreepCount = snapshot.Creeps.Count(creep => creep.SenderId.Equals(playerId));
                builder.Append("\nP").Append(playerId.Value)
                    .Append(" T").Append(towerCount)
                    .Append(" S").Append(sentCreepCount)
                    .Append(" G").Append(player.Gold.Amount)
                    .Append(" I+").Append(player.Income.Amount);
            }

            builder.Append("\nLane flow:");
            var laneCount = snapshot.Players.Players.Count;
            for (var laneId = 1; laneId <= laneCount; laneId++)
            {
                var lane = new LaneId(laneId);
                var towerCount = snapshot.Towers.Count(tower => tower.LaneId.Equals(lane));
                var creeps = snapshot.Creeps.Where(creep => creep.LaneId.Equals(lane)).ToArray();
                builder.Append("\nL").Append(laneId)
                    .Append(" T").Append(towerCount)
                    .Append(" C").Append(creeps.Length);

                if (creeps.Length > 0)
                {
                    var senders = creeps.Select(creep => creep.SenderId.Value).Distinct().OrderBy(senderId => senderId);
                    builder.Append(" <-P").Append(string.Join(",P", senders));
                }
            }

            if (simulationDriver.LatestBotDiagnostics is { } diagnostics)
            {
                if (diagnostics.RecentDecisions.Count > 0)
                {
                    builder.Append("\nBot sends:");
                    foreach (var decision in diagnostics.RecentDecisions)
                    {
                        builder.Append(" P").Append(decision.PlayerId.Value)
                            .Append("x").Append(decision.Quantity)
                            .Append("@").Append(decision.Tick.Value);
                    }
                }
            }

            if (simulationDriver.LatestMatchSummary is { } summary)
            {
                builder.Append("\nWinner: P").Append(summary.WinnerId.Value)
                    .Append(" at tick ").Append(summary.CompletedAtTick.Value);
            }

            LatestText = builder.ToString();
        }

        private void OnGUI()
        {
            if (!ShouldDrawOverlay() || string.IsNullOrEmpty(LatestText))
            {
                return;
            }

            overlayStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                wordWrap = false
            };

            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 420f), GUI.skin.box);
            GUILayout.Label(LatestText, overlayStyle);
            GUILayout.EndArea();
        }
    }
}
