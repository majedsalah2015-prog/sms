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

    /// <summary>Why a section will not take the change — capacity against who is already in it, or history against removal.</summary>
    public enum SectionInUseReason
    {
        /// <summary>The new capacity is below the number of students already assigned.</summary>
        CapacityBelowAssigned = 1,

        /// <summary>Membership history exists — the section is closed, never deleted (BR-SCN-007).</summary>
        HasHistory = 2,

        /// <summary>Something outside the section's own tables still points at it.</summary>
        ReferencedElsewhere = 3,
    }

    /// <summary>
    /// A section can only be edited/deleted while its history (memberships, homeroom assignments,
    /// timetable) allows it.
    /// <para>
    /// The reason used to be an English clause the caller composed, and the Web boundary kept it
    /// verbatim inside an Arabic frame — half a sentence in the reader's language. It is a value
    /// now, with the two numbers the capacity case needs, so the whole sentence can be said in
    /// either language.
    /// </para>
    /// </summary>
    public class SectionInUseException : InvalidOperationException
    {
        public SectionInUseException(int sectionId, SectionInUseReason reason, int requested = 0, int existing = 0)
            : base($"Section {sectionId} is in use: {Describe(reason, requested, existing)}.")
        {
            SectionId = sectionId;
            Reason = reason;
            Requested = requested;
            Existing = existing;
        }

        /// <summary>
        /// The overload for a database refusal on a foreign key nothing checked in advance. The
        /// provider's message stays in the inner exception, where a log can read it, instead of
        /// being spliced into a sentence shown to a registrar.
        /// </summary>
        public SectionInUseException(int sectionId, SectionInUseReason reason, Exception inner)
            : base($"Section {sectionId} is in use: {Describe(reason, 0, 0)}.", inner)
        {
            SectionId = sectionId;
            Reason = reason;
        }

        public int SectionId { get; }

        public SectionInUseReason Reason { get; }

        /// <summary>The capacity that was asked for, when capacity is what was refused.</summary>
        public int Requested { get; }

        /// <summary>How many students are already in the section, or how many history rows exist.</summary>
        public int Existing { get; }

        private static string Describe(SectionInUseReason reason, int requested, int existing) => reason switch
        {
            SectionInUseReason.CapacityBelowAssigned => $"capacity {requested} is below the {existing} currently assigned student(s)",
            SectionInUseReason.HasHistory => $"{existing} membership or homeroom record(s) exist — close the section instead (BR-SCN-007)",
            _ => "other records still reference it",
        };
    }
}
