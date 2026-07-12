using LTW.UnityClient.Simulation;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class SendDockController : MonoBehaviour
    {
        [SerializeField]
        private UnityCommandAdapter commandAdapter = null!;

        [SerializeField]
        private PlacementFeedbackView feedbackView = null!;

        public void SendRunner() => Send(commandAdapter.SendSampleCreep(), "Runner sent");

        public void SendBrute() => Send(commandAdapter.SendBruteCreep(), "Brute sent");

        public void SendSwarm() => Send(commandAdapter.SendSwarmCreep(), "Swarm sent");

        private void Send(LTW.Simulation.Bridge.VerticalSliceCommandResult result, string successMessage)
        {
            if (result.Accepted)
            {
                feedbackView.ShowEconomy(successMessage);
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }
    }
}
