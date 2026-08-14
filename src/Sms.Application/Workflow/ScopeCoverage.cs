using System.Linq;
using Sms.Application.Security;

namespace Sms.Application.Workflow
{
    /// <summary>
    /// Whether an approver's effective scope covers a record (BR-WF-004).
    /// Null id sets are unrestricted (doc 06 §4.3). Dynamic grants (active
    /// year / own sections) count as covering here; their concrete resolution
    /// tightens when the session context lands (E-003 remaining slices).
    /// </summary>
    public static class ScopeCoverage
    {
        public static bool Covers(EffectiveScope scope, WorkflowRecordScope record)
        {
            // An own-records-only approver has no authority over others' records.
            if (scope.OwnRecordsOnly)
            {
                return false;
            }

            if (scope.SchoolIds != null && !scope.SchoolIds.Contains(record.SchoolId))
            {
                return false;
            }

            if (record.AcademicYearId is int year
                && scope.AcademicYearIds != null
                && !scope.AcademicYearIds.Contains(year)
                && !scope.IncludesDynamicActiveYear)
            {
                return false;
            }

            if (record.GradeId is int grade
                && scope.GradeIds != null
                && !scope.GradeIds.Contains(grade))
            {
                return false;
            }

            if (record.SectionId is int section
                && scope.SectionIds != null
                && !scope.SectionIds.Contains(section)
                && !scope.IncludesDynamicOwnSections)
            {
                return false;
            }

            return true;
        }
    }
}
