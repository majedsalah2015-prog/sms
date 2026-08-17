namespace Sms.Application.Timetable
{
    /// <summary>Pure BR-TTB-003: an offering's placement count within a section must equal its weekly-periods plan (BR-SUB-005) before a version can validate/publish.</summary>
    public static class PlacementCompletenessEvaluator
    {
        public static bool IsComplete(int placedCount, int weeklyPeriods) => placedCount == weeklyPeriods;

        public static int Shortfall(int placedCount, int weeklyPeriods) => weeklyPeriods - placedCount;
    }
}
