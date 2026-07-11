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
            textMesh.characterSize = 0.25f;
            textMesh.fontSize = 52;
            textMesh.color = Color.white;
            transform.position = new Vector3(2.5f, 1.2f, 4f);
            transform.rotation = Quaternion.Euler(75f, 0f, 0f);
        }

        private void Update()
        {
            var summary = simulationDriver.LatestMatchSummary;
            textMesh.text = summary is null ? string.Empty : $"MATCH COMPLETE\nPlayer {summary.WinnerId.Value} wins";
        }
    }
}
