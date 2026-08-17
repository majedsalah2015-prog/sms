namespace Sms.Application.Notifications
{
    public class BudgetCheckResult
    {
        public int CurrentCount { get; set; }

        public bool ShouldAlert { get; set; }

        public bool ShouldBlock { get; set; }
    }
}
