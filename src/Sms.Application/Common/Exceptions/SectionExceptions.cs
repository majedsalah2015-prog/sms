using System;
using Sms.Domain.Grades;

namespace Sms.Application.Common.Exceptions
{
    // Each of these carries its values as typed properties as well as in the message.
    // The message is English by design — that is what a log line should say in every
    // deployment — and Sms.Web/Models/UserMessage.cs rebuilds the sentence in the
    // reader's language from the properties rather than by parsing the text.

    /// <summary>BR-SCN-001: section names are unique within grade + year.</summary>
    public class DuplicateSectionNameException : InvalidOperationException
    {
        public DuplicateSectionNameException(string name)
            : base($"A section named '{name}' already exists for this grade and year (BR-SCN-001).")
        {
            Name = name;
        }

        public string Name { get; }
    }

    /// <summary>BR-SCN-002: a section's capacity can't exceed its grade's planned section size.</summary>
    public class SectionCapacityPlanExceededException : InvalidOperationException
    {
        public SectionCapacityPlanExceededException(int requestedCapacity, int gradeTargetSectionSize)
            : base($"Section capacity {requestedCapacity} exceeds the grade's planned section size {gradeTargetSectionSize} (BR-SCN-002).")
        {
            RequestedCapacity = requestedCapacity;
            GradeTargetSectionSize = gradeTargetSectionSize;
        }

        public int RequestedCapacity { get; }

        public int GradeTargetSectionSize { get; }
    }

    /// <summary>BR-SCN-002: assigning beyond the section's own capacity needs a permission-gated override — not available in this slice.</summary>
    public class SectionFullException : InvalidOperationException
    {
        public SectionFullException(int sectionId)
            : base($"Section {sectionId} is at capacity (BR-SCN-002).")
        {
            SectionId = sectionId;
        }

        public int SectionId { get; }
    }

    /// <summary>BR-SCN-003: a section's gender policy must narrow its grade's, never widen it.</summary>
    public class InvalidSectionGenderPolicyException : InvalidOperationException
    {
        public InvalidSectionGenderPolicyException(GenderPolicy gradePolicy, GenderPolicy requestedPolicy)
            : base($"'{requestedPolicy}' does not narrow the grade's '{gradePolicy}' policy (BR-SCN-003).")
        {
            GradePolicy = gradePolicy;
            RequestedPolicy = requestedPolicy;
        }

        public GenderPolicy GradePolicy { get; }

        public GenderPolicy RequestedPolicy { get; }
    }

    /// <summary>BR-SCN-007: closing a section requires zero currently assigned students.</summary>
    public class SectionCloseWithMembersException : InvalidOperationException
    {
        public SectionCloseWithMembersException(int sectionId, int memberCount)
            : base($"Section {sectionId} still has {memberCount} assigned student(s); transfer them before closing (BR-SCN-007).")
        {
            SectionId = sectionId;
            MemberCount = memberCount;
        }

        public int SectionId { get; }

        public int MemberCount { get; }
    }

    /// <summary>A section can only be edited/deleted while its history (memberships, homeroom assignments, timetable) allows it.</summary>
    public class SectionInUseException : InvalidOperationException
    {
        public SectionInUseException(int sectionId, string reason)
            : base($"Section {sectionId} is in use: {reason}.")
        {
            SectionId = sectionId;
            Reason = reason;
        }

        public int SectionId { get; }

        /// <summary>An English clause the caller composed. UserMessage translates the frame and keeps the clause — half a sentence in the reader's language beats none.</summary>
        public string Reason { get; }
    }
}
