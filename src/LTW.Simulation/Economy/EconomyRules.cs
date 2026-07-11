namespace LTW.Simulation.Economy;

public sealed class EconomyRules
{
    public EconomyRules(
        int incomeIntervalTicks,
        int sendCooldownTicks,
        int sellRefundPercent,
        int leakLifeLoss)
    {
        if (incomeIntervalTicks <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(incomeIntervalTicks), "Income interval must be positive.");
        }

        if (sendCooldownTicks < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(sendCooldownTicks), "Send cooldown cannot be negative.");
        }

        if (sellRefundPercent < 0 || sellRefundPercent > 100)
        {
            throw new System.ArgumentOutOfRangeException(nameof(sellRefundPercent), "Sell refund percent must be between 0 and 100.");
        }

        if (leakLifeLoss <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(leakLifeLoss), "Leak life loss must be positive.");
        }

        IncomeIntervalTicks = incomeIntervalTicks;
        SendCooldownTicks = sendCooldownTicks;
        SellRefundPercent = sellRefundPercent;
        LeakLifeLoss = leakLifeLoss;
    }

    public int IncomeIntervalTicks { get; }

    public int SendCooldownTicks { get; }

    public int SellRefundPercent { get; }

    public int LeakLifeLoss { get; }
}
