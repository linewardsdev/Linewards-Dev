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

        private bool initialized;

        private void Awake()
        {
            TryInitialize();
        }

        public void Initialize(UnitySimulationDriver driver, UnityCommandAdapter adapter)
        {
            simulationDriver = driver;
            commandAdapter = adapter;
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (initialized || simulationDriver == null || commandAdapter == null)
            {
                return;
            }

            var simulation = new LocalVerticalSlice(SampleVerticalSliceContent.Create());
            simulationDriver.Initialize(simulation);
            commandAdapter.Initialize(simulation, simulationDriver);
            initialized = true;
        }
    }
}
