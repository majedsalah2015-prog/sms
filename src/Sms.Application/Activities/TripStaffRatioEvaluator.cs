namespace Sms.Application.Activities
{
    /// <summary>Pure BR-ACT-004: staff-ratio rule (e.g. 1:10 KG) — required staff rounds up (a partial group still needs a full extra supervisor).</summary>
    public static class TripStaffRatioEvaluator
    {
        public static int RequiredStaff(int studentCount, int ratioRequired)
        {
            if (ratioRequired <= 0 || studentCount <= 0)
            {
                return 0;
            }

            return (studentCount + ratioRequired - 1) / ratioRequired;
        }

        public static bool IsSatisfied(int studentCount, int assignedStaffCount, int ratioRequired)
            => assignedStaffCount >= RequiredStaff(studentCount, ratioRequired);
    }
}
