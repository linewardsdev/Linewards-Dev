#nullable enable

using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class LocalSessionFlowOverlay : MonoBehaviour
    {
        public void Initialize(UnitySimulationDriver driver, LocalPlaytestRecorder recorder)
        {
            // Match flow is intentionally hotkey-only so the bottom touch area stays
            // dedicated to build/send controls on mobile-sized screens.
        }
    }
}
