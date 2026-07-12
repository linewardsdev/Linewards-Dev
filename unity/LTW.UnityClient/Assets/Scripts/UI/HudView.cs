using LTW.Simulation.Bridge;
using LTW.Simulation.Primitives;
using UnityEngine;

namespace LTW.UnityClient.UI
{
    public sealed class HudView : MonoBehaviour
    {
        private const long IncomeIntervalTicks = 50;
        public string GoldText { get; private set; } = "0";

        public string IncomeText { get; private set; } = "0";

        public string LivesText { get; private set; } = "0";

        public string PressureText { get; private set; } = "0";

        public string IncomeTimerText { get; private set; } = "50";

        public string LaneText { get; private set; } = "Your Line";

        public bool IncomeTickSoon { get; private set; }

        public void Render(VerticalSliceSnapshot snapshot)
        {
            var player = snapshot.Players.Get(new PlayerId(1));
            GoldText = player.Gold.Amount.ToString();
            IncomeText = player.Income.Amount.ToString();
            LivesText = player.Lives.Amount.ToString();
            PressureText = snapshot.Creeps.Count.ToString();
            var ticksUntilIncome = IncomeIntervalTicks - snapshot.Tick.Value % IncomeIntervalTicks;
            IncomeTimerText = ticksUntilIncome.ToString();
            IncomeTickSoon = ticksUntilIncome <= 5;
            LaneText = "Your Line";
        }
    }
}
