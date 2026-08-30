#nullable enable

using System.Linq;
using System.Text;
using LTW.Simulation.Primitives;
using LTW.UnityClient.UI;
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
        /// LatestText is still produced either way — the claim here used to be that the playtest
        /// recorder consumes it, and it does not: nothing outside this class reads LatestText.
        /// Corrected rather than deleted because it is the reason Update runs while the panel is
        /// hidden, and that reason is now "so a diagnostics run does not have to be restarted",
        /// which is a much weaker one than a consumer. It costs a rebuild per snapshot since
        /// OPEN_ITEMS item 36; it cost one per frame before.
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

        /// <summary>
        /// The driver revision <see cref="LatestText"/> was built from. <see cref="long.MinValue"/>
        /// means nothing has been built yet, which no real revision is.
        /// </summary>
        private long renderedSnapshotRevision = long.MinValue;

        public string LatestText { get; private set; } = string.Empty;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
        }

        /// <summary>
        /// Rebuilds the overlay text, once per snapshot rather than once per frame.
        /// </summary>
        /// <remarks>
        /// Everything below is a pure function of the snapshot, the bot diagnostics and the match
        /// summary, all of which move together with the driver's revision, so running it at ~60 fps
        /// against a 4 Hz simulation produced fifteen identical strings per input change — a
        /// StringBuilder, eight LINQ counts per seat, a filtered creep array per lane and a
        /// string.Join, every frame, for a panel that is off unless -ltwDiagnostics was passed.
        ///
        /// It is gated here rather than left to the driver because
        /// <c>UnitySimulationDriver.LatestBotDiagnostics</c> is now built on demand (OPEN_ITEMS item
        /// 36): reading it every frame would have moved that allocation from the driver into here
        /// rather than removing it. Same gate the driver uses and the same one the renderer's hash
        /// answers, so the three cannot disagree about what is new.
        /// </remarks>
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

            if (simulationDriver.SnapshotRevision == renderedSnapshotRevision)
            {
                return;
            }

            renderedSnapshotRevision = simulationDriver.SnapshotRevision;

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
                font = RuntimeUiChrome.SharedFont,
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
