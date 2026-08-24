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
    }
}
