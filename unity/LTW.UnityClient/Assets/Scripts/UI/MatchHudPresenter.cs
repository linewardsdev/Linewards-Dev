using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class MatchHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private HudView hudView = null!;

        private void Update()
        {
            var snapshot = simulationDriver.LatestSnapshot;
            if (snapshot is null)
            {
                return;
            }

            hudView.Render(snapshot);
        }
    }
}
