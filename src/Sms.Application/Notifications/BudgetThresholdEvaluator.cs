namespace Sms.Application.Notifications
{
    /// <summary>Pure BR-NTF-004: alert at 80% of the period budget; optional hard-stop at 100% — safety-class messages are exempt from the hard-stop (never block a not-boarded alert on budget).</summary>
    public static class BudgetThresholdEvaluator
    {
        public static bool ShouldAlert(int messageCount, int budgetLimit)
            => budgetLimit > 0 && messageCount >= budgetLimit * 0.8m;

        public static bool ShouldBlock(int messageCount, int budgetLimit, bool isSafetyClass)
            => !isSafetyClass && budgetLimit > 0 && messageCount >= budgetLimit;
    }
}
