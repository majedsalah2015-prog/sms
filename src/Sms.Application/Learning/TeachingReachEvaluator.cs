using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure BR-LRN-002: who may put content or homework in front of which
    /// students. Three widening bands and no fourth —
    ///
    ///  1. a teacher reaches the (offering, section) pairs they hold a Placement
    ///     on in the *published* timetable version;
    ///  2. a Head of Department reaches every offering in their Department;
    ///  3. Vice-Principal and above reach the school.
    ///
    /// "There is no 'all sections' issue path below Vice-Principal" is the point
    /// of the rule, so <paramref name="hasSchoolWideReach"/> is passed in
    /// explicitly by the caller from a permission check rather than inferred
    /// here from an empty placement list — an absent list means *no* reach, and
    /// must never read as unrestricted reach.
    ///
    /// A lesson is authored per offering (any section the teacher holds of it),
    /// while homework is issued to one named section; hence the two questions.
    /// </summary>
    public static class TeachingReachEvaluator
    {
        /// <summary>BR-LRN-002 for content (doc/Modules/37 §8.1-2): the lesson is anchored on an offering, so holding any section of it is reach enough.</summary>
        public static bool CanAuthorContent(
            IEnumerable<PlacementReach>? placements,
            IEnumerable<int>? departmentOfferingIds,
            bool hasSchoolWideReach,
            int curriculumOfferingId)
        {
            if (hasSchoolWideReach)
            {
                return true;
            }

            if (placements != null && placements.Any(p => p.CurriculumOfferingId == curriculumOfferingId))
            {
                return true;
            }

            return departmentOfferingIds != null && departmentOfferingIds.Contains(curriculumOfferingId);
        }

        /// <summary>BR-LRN-002 for work issued to a named section (doc/Modules/37 §8.3): the pair must be held, not just the offering.</summary>
        public static bool CanIssueToSection(
            IEnumerable<PlacementReach>? placements,
            IEnumerable<int>? departmentOfferingIds,
            bool hasSchoolWideReach,
            int curriculumOfferingId,
            int sectionId)
        {
            if (hasSchoolWideReach)
            {
                return true;
            }

            if (placements != null
                && placements.Any(p => p.CurriculumOfferingId == curriculumOfferingId && p.SectionId == sectionId))
            {
                return true;
            }

            return departmentOfferingIds != null && departmentOfferingIds.Contains(curriculumOfferingId);
        }
    }
}
