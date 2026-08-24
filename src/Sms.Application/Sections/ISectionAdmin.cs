using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Grades;
using Sms.Domain.Sections;

namespace Sms.Application.Sections
{
    /// <summary>doc/Modules/06 §8 "Section list"/"Assignment board"/"Transfer dialog" screens backing (screens deferred, the operations are core).</summary>
    public interface ISectionAdmin
    {
        /// <summary>Throws <see cref="Common.Exceptions.DuplicateSectionNameException"/>, <see cref="Common.Exceptions.SectionCapacityPlanExceededException"/>, or <see cref="Common.Exceptions.InvalidSectionGenderPolicyException"/>.</summary>
        Task<Section> DefineSectionAsync(
            int gradeYearProfileId, string nameAr, string nameEn, int capacity, GenderPolicy genderPolicy,
            int? defaultClassroomId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens several sections at once, named by <see cref="SectionNameSequence"/>
        /// from the grade's own convention (BR-SCN-001). A grade is planned as a number
        /// of sections, not as one section repeated — typing four names by hand is the
        /// step where a school ends up with "1-A", "1-b" and "1 - C" in the same grade.
        /// <para>
        /// Every proposed name and the shared capacity and gender policy are checked
        /// against the whole batch before anything is written, so a count that would
        /// break the grade's plan on the third section refuses all four rather than
        /// leaving two behind. The same exceptions as
        /// <see cref="DefineSectionAsync"/>.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<Section>> DefineSectionsAsync(
            int gradeYearProfileId, int count, int capacity, GenderPolicy genderPolicy,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Edits a section's names/capacity/gender/room under the same rules as
        /// <see cref="DefineSectionAsync"/>; capacity can't drop below the current member count.
        /// </summary>
        Task<Section> UpdateSectionAsync(
            int sectionId, string nameAr, string nameEn, int capacity, GenderPolicy genderPolicy,
            int? defaultClassroomId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hard-deletes a section that never had members or homeroom assignments (a section
        /// with history is closed instead, <see cref="CloseSectionAsync"/>). Throws
        /// <see cref="Common.Exceptions.SectionInUseException"/> otherwise.
        /// </summary>
        Task DeleteSectionAsync(int sectionId, CancellationToken cancellationToken = default);

        /// <summary>BR-SCN-004: closes out the section's current homeroom teacher (if any) and opens a new one.</summary>
        Task<HomeroomAssignment> AssignHomeroomTeacherAsync(
            int sectionId, int teacherUserId, DateTime effectiveFromUtc, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.SectionFullException"/>.</summary>
        Task<SectionMembership> AssignMembershipAsync(
            int sectionId, int enrollmentId, DateTime effectiveFromUtc, CancellationToken cancellationToken = default);

        /// <summary>BR-SCN-005/006: closes the enrollment's current membership and opens one in the target section.</summary>
        Task<SectionMembership> TransferMembershipAsync(
            int enrollmentId, int targetSectionId, string transferReasonCode, DateTime effectiveDate, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.SectionCloseWithMembersException"/> unless the section has zero current members (BR-SCN-007).</summary>
        Task CloseSectionAsync(int sectionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies a whole assignment-board layout in one go (doc/Modules/06 §8.3):
        /// each entry seats an enrollment in a section, opening a membership where
        /// there was none and transferring where there was one.
        /// <para>
        /// The batch is validated as a batch before anything is written. Capacity is
        /// a property of a section after every move lands, not of any single move, so
        /// checking one student at a time would wave through a layout that puts three
        /// children into the two seats that were left — and would leave the first two
        /// written when the third was refused. Gender policy (BR-SCN-003) is checked
        /// per student against the section they are dropped into.
        /// </para>
        /// <para>
        /// Everything commits in one <c>SaveChangesAsync</c>, which is also what keeps
        /// the audit entries for a thirty-student redistribution in one transaction
        /// with the memberships they describe.
        /// </para>
        /// <para>
        /// Throws <see cref="Common.Exceptions.SectionFullException"/>,
        /// <see cref="Common.Exceptions.SectionGenderMismatchException"/> or
        /// <see cref="Common.Exceptions.SectionGradeMismatchException"/>; returns the
        /// number of students actually moved (an entry that names the section a
        /// student already sits in is not a move and writes nothing).
        /// </para>
        /// </summary>
        /// <param name="placements">enrollment id → target section id.</param>
        Task<int> ApplyDistributionAsync(
            IReadOnlyDictionary<int, int> placements, string transferReasonCode, DateTime effectiveDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a section by first moving every student out of it (doc/Modules/06
        /// §8.5, BR-SCN-007). <paramref name="placements"/> must name every one of the
        /// section's current members and may not send any of them back into it.
        /// <para>
        /// The two halves are one operation on purpose. <see cref="CloseSectionAsync"/>
        /// refuses a section that still holds students, so closing one means emptying
        /// it first — and doing that as two calls leaves a real failure mode where
        /// thirty children have been moved and the section they came from is still
        /// open, which is neither the old state nor the new one.
        /// </para>
        /// <para>
        /// The section's open homeroom assignment is closed out at the same date: a
        /// teacher cannot go on being homeroom of a section that no longer runs, and
        /// BR-SCN-004 keeps the assignment as history rather than deleting it.
        /// </para>
        /// <para>
        /// What this does <b>not</b> do is void the section's timetable sessions
        /// forward from the effective date. That is BR-SCN-007's other half and it
        /// belongs to Module 15, which owns sessions; the wizard reports the count so
        /// the decision is made with it in view.
        /// </para>
        /// </summary>
        /// <returns>How many students were moved out.</returns>
        Task<int> MergeAndCloseSectionAsync(
            int sectionId, IReadOnlyDictionary<int, int> placements, string transferReasonCode, DateTime effectiveDate,
            CancellationToken cancellationToken = default);
    }
}
