using LTW.Simulation.Bridge;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class UnityMatchBootstrapper : MonoBehaviour
    {
        [SerializeField]
        private UnitySimulationDriver simulationDriver = null!;

        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        private void Awake()
        {
            var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
            simulationDriver.Initialize(simulation);
            commandAdapter.Initialize(simulation);
        }
    }
}
