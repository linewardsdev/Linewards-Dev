using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class HudView : MonoBehaviour
    {
        public string GoldText { get; private set; } = "0";

        public string IncomeText { get; private set; } = "0";

        public string LivesText { get; private set; } = "0";

        public string PressureText { get; private set; } = "0";

        public void Render(VerticalSliceSnapshot snapshot)
        {
            var player = snapshot.Players.Get(new PlayerId(1));
            GoldText = player.Gold.Amount.ToString();
            IncomeText = player.Income.Amount.ToString();
            LivesText = player.Lives.Amount.ToString();
            PressureText = snapshot.Creeps.Count.ToString();
        }
    }
}
