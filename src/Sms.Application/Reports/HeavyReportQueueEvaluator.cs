namespace Sms.Application.Reports
{
    /// <summary>Pure BR-RPT-005: reports at or above the configured row-count threshold run queued rather than interactive (NF-P5's ≤10s target only applies below the threshold).</summary>
    public static class HeavyReportQueueEvaluator
    {
        public static bool ShouldQueue(int estimatedRowCount, int threshold) => estimatedRowCount >= threshold;
    }
}
