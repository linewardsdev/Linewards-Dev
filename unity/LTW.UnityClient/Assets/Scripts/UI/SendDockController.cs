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

        public void SendRunner() => Send(commandAdapter.SendSampleCreep());

        public void SendBrute() => Send(commandAdapter.SendBruteCreep());

        public void SendSwarm() => Send(commandAdapter.SendSwarmCreep());

        private void Send(LTW.Simulation.Bridge.VerticalSliceCommandResult result)
        {
            if (result.Accepted)
            {
                feedbackView.Clear();
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }
    }
}
