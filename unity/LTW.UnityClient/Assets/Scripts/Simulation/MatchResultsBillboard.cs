using System.Text;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class MatchResultsBillboard : MonoBehaviour
    {
        private UnitySimulationDriver simulationDriver = null!;
        private TextMesh textMesh = null!;

        public void Initialize(UnitySimulationDriver driver)
        {
            simulationDriver = driver;
            textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.22f;
            textMesh.fontSize = 46;
            textMesh.color = Color.white;
            transform.position = new Vector3(3f, 1.35f, 9f);
            transform.rotation = Quaternion.Euler(75f, 0f, 0f);
        }

        private void Update()
        {
            var summary = simulationDriver.LatestMatchSummary;
            if (summary is null)
            {
                textMesh.text = string.Empty;
                return;
            }

            var builder = new StringBuilder();
            builder.Append("MATCH COMPLETE\n")
                .Append("P").Append(summary.WinnerId.Value).Append(" wins at tick ")
                .Append(summary.CompletedAtTick.Value);

            foreach (var player in summary.Players)
            {
                builder.Append("\nP").Append(player.PlayerId.Value)
                    .Append(player.IsEliminated ? " OUT" : " ACTIVE")
                    .Append("  Lives ").Append(player.Lives.Amount)
                    .Append("  Income ").Append(player.Income.Amount)
                    .Append("  Gold ").Append(player.Gold.Amount);
            }

            textMesh.text = builder.ToString();
        }
    }
}
