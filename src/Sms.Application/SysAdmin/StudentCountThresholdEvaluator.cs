namespace Sms.Application.SysAdmin
{
    /// <summary>Pure BR-SYS-006: approaching-limit warning as the student count nears the license's cap.</summary>
    public static class StudentCountThresholdEvaluator
    {
        public static bool IsApproachingLimit(int currentCount, int cap, int warningPercent = 90)
            => cap > 0 && currentCount * 100 >= cap * warningPercent;

        public static bool IsOverCap(int currentCount, int cap) => currentCount > cap;
    }
}
