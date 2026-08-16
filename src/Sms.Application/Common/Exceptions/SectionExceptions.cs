using System;
using Sms.Domain.Grades;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-SCN-001: section names are unique within grade + year.</summary>
    public class DuplicateSectionNameException : InvalidOperationException
    {
        public DuplicateSectionNameException(string name)
            : base($"A section named '{name}' already exists for this grade and year (BR-SCN-001).")
        {
        }
    }

    /// <summary>BR-SCN-002: a section's capacity can't exceed its grade's planned section size.</summary>
    public class SectionCapacityPlanExceededException : InvalidOperationException
    {
        public SectionCapacityPlanExceededException(int requestedCapacity, int gradeTargetSectionSize)
            : base($"Section capacity {requestedCapacity} exceeds the grade's planned section size {gradeTargetSectionSize} (BR-SCN-002).")
        {
        }
    }

    /// <summary>BR-SCN-002: assigning beyond the section's own capacity needs a permission-gated override — not available in this slice.</summary>
    public class SectionFullException : InvalidOperationException
    {
        public SectionFullException(int sectionId)
            : base($"Section {sectionId} is at capacity (BR-SCN-002).")
        {
        }
    }

    /// <summary>BR-SCN-003: a section's gender policy must narrow its grade's, never widen it.</summary>
    public class InvalidSectionGenderPolicyException : InvalidOperationException
    {
        public InvalidSectionGenderPolicyException(GenderPolicy gradePolicy, GenderPolicy requestedPolicy)
            : base($"'{requestedPolicy}' does not narrow the grade's '{gradePolicy}' policy (BR-SCN-003).")
        {
        }
    }

    /// <summary>BR-SCN-007: closing a section requires zero currently assigned students.</summary>
    public class SectionCloseWithMembersException : InvalidOperationException
    {
        public SectionCloseWithMembersException(int sectionId, int memberCount)
            : base($"Section {sectionId} still has {memberCount} assigned student(s); transfer them before closing (BR-SCN-007).")
        {
        }
    }
}
