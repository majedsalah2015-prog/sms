namespace Sms.Application.Sections
{
    /// <summary>
    /// Pure BR-SCN-002 checks. The permission-gated override for exceeding
    /// capacity is deferred (no permission-check integration in this slice,
    /// same as every other admin service so far) — callers currently get a
    /// hard rejection, not an override path.
    /// </summary>
    public static class SectionCapacityGuard
    {
        /// <summary>A section's own capacity can't exceed its grade's planned section size.</summary>
        public static bool WithinGradePlan(int sectionCapacity, int gradeTargetSectionSize)
            => sectionCapacity <= gradeTargetSectionSize;

        public static bool CanAssign(int currentMemberCount, int sectionCapacity)
            => currentMemberCount < sectionCapacity;
    }
}
