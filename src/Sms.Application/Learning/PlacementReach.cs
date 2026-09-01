namespace Sms.Application.Learning
{
    /// <summary>
    /// One (offering, section) pair a teacher holds in the *published* timetable
    /// version — the unit BR-LRN-002 measures reach in. A value type so the
    /// engine stays pure: the caller reads Module 15's placements, this decides.
    /// </summary>
    public readonly struct PlacementReach
    {
        public PlacementReach(int curriculumOfferingId, int sectionId)
        {
            CurriculumOfferingId = curriculumOfferingId;
            SectionId = sectionId;
        }

        public int CurriculumOfferingId { get; }

        public int SectionId { get; }
    }
}
