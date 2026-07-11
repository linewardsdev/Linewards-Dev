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

        public void SendRunner()
        {
            var result = commandAdapter.SendSampleCreep();
            if (result.Accepted)
            {
                feedbackView.Clear();
                return;
            }

            feedbackView.ShowRejected(result.RejectionReason);
        }
    }
}
