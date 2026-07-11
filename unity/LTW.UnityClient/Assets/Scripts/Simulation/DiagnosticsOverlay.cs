using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        private void OnGUI()
        {
            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            GUILayout.Label($"Tick: {snapshot.Tick.Value}");
            foreach (var player in snapshot.Players.Players)
            {
                GUILayout.Label($"P{player.PlayerId.Value} Gold:{player.Gold.Amount} Income:{player.Income.Amount} Lives:{player.Lives.Amount}");
            }

            GUILayout.Label($"Towers: {snapshot.Towers.Count} Creeps: {snapshot.Creeps.Count}");
        }
    }
}
