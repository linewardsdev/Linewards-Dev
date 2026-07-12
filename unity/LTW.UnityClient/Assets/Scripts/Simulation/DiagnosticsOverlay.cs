using System.Text;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private bool showRuntimeOverlay = true;

        private GUIStyle? overlayStyle;

        public string LatestText { get; private set; } = string.Empty;

        private void Update()
        {
            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append("Tick: ").Append(snapshot.Tick.Value);
            foreach (var player in snapshot.Players.Players)
            {
                builder.Append("\nP").Append(player.PlayerId.Value)
                    .Append(" Gold:").Append(player.Gold.Amount)
                    .Append(" Income:").Append(player.Income.Amount)
                    .Append(" Lives:").Append(player.Lives.Amount);
            }

            builder.Append("\nTowers: ").Append(snapshot.Towers.Count)
                .Append(" Creeps: ").Append(snapshot.Creeps.Count)
                .Append("\nFX: ").Append(PresentationPreferences.ReducedEffects ? "reduced" : "full")
                .Append(" Audio: ").Append(PresentationPreferences.AudioMuted ? "muted" : Mathf.RoundToInt(PresentationPreferences.FeedbackVolume * 100f) + "%");

            if (simulationDriver.LatestBotDiagnostics is { } diagnostics)
            {
                builder.Append("\nBots:");
                foreach (var profile in diagnostics.Profiles)
                {
                    builder.Append("\n  P").Append(profile.PlayerId.Value)
                        .Append(" ").Append(profile.Profile)
                        .Append(" sends ").Append(profile.PrimaryCreepId.Value);
                }

                if (diagnostics.RecentDecisions.Count > 0)
                {
                    var latest = diagnostics.RecentDecisions[diagnostics.RecentDecisions.Count - 1];
                    builder.Append("\nLast bot send: P").Append(latest.PlayerId.Value)
                        .Append(" x").Append(latest.Quantity)
                        .Append(" ").Append(latest.ContentId.Value)
                        .Append(" @").Append(latest.Tick.Value);
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
            if (!showRuntimeOverlay || string.IsNullOrEmpty(LatestText))
            {
                return;
            }

            overlayStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                wordWrap = false
            };

            GUILayout.BeginArea(new Rect(12f, 12f, 360f, 230f), GUI.skin.box);
            GUILayout.Label(LatestText, overlayStyle);
            GUILayout.EndArea();
        }
    }
}
