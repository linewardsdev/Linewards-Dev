using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        public string LatestText { get; private set; } = string.Empty;

        private void Update()
        {
            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            var text = $"Tick: {snapshot.Tick.Value}";
            foreach (var player in snapshot.Players.Players)
            {
                text += $"\nP{player.PlayerId.Value} Gold:{player.Gold.Amount} Income:{player.Income.Amount} Lives:{player.Lives.Amount}";
            }

            LatestText = $"{text}\nTowers: {snapshot.Towers.Count} Creeps: {snapshot.Creeps.Count}";
        }
    }
}
