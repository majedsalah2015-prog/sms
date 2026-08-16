namespace Sms.Application.Teachers
{
    /// <summary>Pure BR-TCH-004: load = sum of assigned offerings' weekly periods; over-max requires a logged override, under-loading is reported not blocked.</summary>
    public static class TeacherLoadCalculator
    {
        public static int CurrentLoad(int[] assignedOfferingWeeklyPeriods)
        {
            var total = 0;
            foreach (var periods in assignedOfferingWeeklyPeriods)
            {
                total += periods;
            }

            return total;
        }

        public static bool ExceedsMax(int currentLoad, int additionalPeriods, int maxWeeklyPeriods)
            => currentLoad + additionalPeriods > maxWeeklyPeriods;
    }
}
