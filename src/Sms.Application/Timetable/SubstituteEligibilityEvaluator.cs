namespace Sms.Application.Timetable
{
    /// <summary>Pure BR-TTB-007: a substitute must be free at the slot, and either qualified (BR-SUB-006 matrix) or explicitly allowed to supervise-only (doc §14 open question #2, shipped as a flagged option).</summary>
    public static class SubstituteEligibilityEvaluator
    {
        public static bool IsEligible(bool isFreeAtSlot, bool isQualified, bool allowSuperviseOnly)
            => isFreeAtSlot && (isQualified || allowSuperviseOnly);
    }
}
