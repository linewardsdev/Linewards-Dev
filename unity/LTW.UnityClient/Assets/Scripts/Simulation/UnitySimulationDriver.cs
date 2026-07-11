using System.Collections.Generic;
using LTW.Simulation.Bridge;
using LTW.Simulation.Events;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    public sealed class UnitySimulationDriver : MonoBehaviour
    {
        [SerializeField]
        private float ticksPerSecond = 10f;

        private LocalVerticalSlice simulation;
        private float accumulator;

        public VerticalSliceSnapshot LatestSnapshot { get; private set; }

        public IReadOnlyList<ISimulationEvent> LatestEvents { get; private set; } = new List<ISimulationEvent>();

        public void Initialize(LocalVerticalSlice localSimulation)
        {
            simulation = localSimulation;
            LatestSnapshot = simulation.GetSnapshot();
        }

        private void Update()
        {
            if (simulation is null)
            {
                return;
            }

            accumulator += Time.deltaTime;
            var tickDuration = 1f / ticksPerSecond;
            while (accumulator >= tickDuration)
            {
                simulation.AdvanceOneTick();
                accumulator -= tickDuration;
            }

            LatestSnapshot = simulation.GetSnapshot();
            LatestEvents = simulation.DrainEvents();
        }

        public void ResetMatch()
        {
            simulation?.Reset();
            if (simulation is not null)
            {
                LatestSnapshot = simulation.GetSnapshot();
            }
        }
    }
}
