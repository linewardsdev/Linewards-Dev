using LTW.Simulation.Commands;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class PlacementFeedbackView : MonoBehaviour
    {
        public string FeedbackText { get; private set; } = string.Empty;

        public void ShowAccepted()
        {
            FeedbackText = "Placed";
        }

        public void ShowRejected(CommandRejectionReason reason)
        {
            FeedbackText = reason switch
            {
                CommandRejectionReason.InsufficientGold => "Need gold",
                CommandRejectionReason.CellOccupied => "Occupied",
                CommandRejectionReason.PathBlocked => "Path blocked",
                CommandRejectionReason.InvalidLane => "Invalid cell",
                _ => "Cannot place"
            };
        }

        public void Clear()
        {
            FeedbackText = string.Empty;
        }
    }
}
