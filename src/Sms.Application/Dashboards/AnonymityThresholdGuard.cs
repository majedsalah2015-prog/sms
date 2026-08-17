namespace Sms.Application.Dashboards
{
    /// <summary>Pure BR-DSH-007: aggregate widgets over restricted categories (discipline, medical, salary) mask small counts below a configured threshold rather than showing an exact, potentially-identifying number.</summary>
    public static class AnonymityThresholdGuard
    {
        public static bool ShouldMask(int count, int threshold) => count > 0 && count < threshold;

        /// <summary>Null = masked (caller renders "fewer than {threshold}"); otherwise the real count.</summary>
        public static int? Mask(int count, int threshold) => ShouldMask(count, threshold) ? (int?)null : count;
    }
}
