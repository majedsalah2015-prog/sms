using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Common;
using Sms.Domain.Students;

namespace Sms.Application.Students
{
    /// <summary>
    /// doc/Modules/10 §8 Student File screens backing (screens deferred, the
    /// operations are core). Issues the student's permanent number via
    /// E-006's INumberIssuer (series "STU") — the number materializes only
    /// on this method's own commit (BR-NUM-003).
    /// </summary>
    public interface IStudentAdmin
    {
        Task<Student> RegisterStudentAsync(
            string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The family's social profile — a separate operation from
        /// <see cref="UpdateStudentAsync"/>, not extra parameters on it.
        /// <para>
        /// The parents used to lead this list — the mother's own particulars, and both
        /// parents' life status. All of it is guardian data now (owner request,
        /// 2026-08-24): a <c>Parent</c> row linked by <see cref="LinkGuardianAsync"/>
        /// with relationship "Father" or "Mother", carrying the name, ID number, mobile,
        /// occupation, qualification and <c>LifeStatus</c> on one row per person instead
        /// of one copy per child.
        /// </para>
        /// <para>
        /// They are filled at a different time by a different person: identity comes
        /// off the birth certificate at registration, this section off documents that
        /// arrive over the following weeks, and often from the social worker rather
        /// than the registrar. One signature covering both would be twenty-five
        /// parameters where a caller correcting a surname has to restate the ration
        /// card number, and would make "who changed the means assessment" unanswerable
        /// from the shape of the call.
        /// </para>
        /// <para>
        /// Every field is nullable and none is required. A registrar must be able to
        /// record a student before the paperwork exists; the alternative is a blocked
        /// enrolment or invented data, and the second is worse.
        /// </para>
        /// </summary>
        Task<Student> UpdateSocialProfileAsync(
            int studentId,
            Religion? religion,
            ResidencyStatus? residencyStatus, FinancialStatus? financialStatus, string? rationCardNo,
            string? placeOfBirth, int? familySize, int? birthOrder, int? siblingCount, string? mobile,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Where the student lives (owner request, 2026-08-31): the locality and, where the locality
        /// has quarters at all, the quarter inside it. The governorate is walked up from the locality
        /// rather than passed, so a selection cannot name a governorate its locality does not sit in.
        /// <para>
        /// Its own method rather than four more parameters on
        /// <see cref="UpdateSocialProfileAsync"/>: a family moves house without any of its social
        /// standing changing, and the residence sits on the Personal tab where the social profile is
        /// permission-gated away from most of the people who record an address.
        /// </para>
        /// <para>
        /// Passing both as null clears the residence — a record that turned out to be wrong has to be
        /// removable, and there is no delete verb to do it with (BR-GLB-005).
        /// </para>
        /// <para>
        /// Throws <see cref="Common.Exceptions.InvalidResidenceSelectionException"/> for a quarter
        /// with no locality beside it, or a quarter belonging to some other locality.
        /// </para>
        /// </summary>
        Task SetResidenceAsync(int studentId, int? residenceAreaId, int? neighbourhoodId, CancellationToken cancellationToken = default);

        /// <summary>Corrects identity/ID fields; identity edits are T1 with a mandatory audit reason (BR-STU-002).</summary>
        Task<Student> UpdateStudentAsync(
            int studentId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidStudentStatusTransitionException"/>.</summary>
        Task ChangeStatusAsync(int studentId, StudentStatus newStatus, CancellationToken cancellationToken = default);

        Task<StudentGuardianLink> LinkGuardianAsync(
            int studentId, int parentId, int relationshipLookupId, bool isPrimaryContact, bool isFinanciallyResponsible,
            bool isPickupAuthorized, bool isPortalVisible, DateTime effectiveFromUtc, int? guardianshipDocAttachmentId = null,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.LastFinanciallyResponsibleGuardianException"/> if this would leave the student with none.</summary>
        Task UnlinkGuardianAsync(int linkId, DateTime effectiveToUtc, CancellationToken cancellationToken = default);

        Task<EmergencyContact> AddEmergencyContactAsync(
            int studentId, string nameAr, string nameEn, string phone, bool isPickupAuthorized,
            int? relationshipLookupId = null, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.DuplicateEnrollmentException"/> (BR-GLB-024).</summary>
        Task<Enrollment> EnrollAsync(
            int studentId, int gradeYearProfileId, DateTime enrollmentDate, EnrollmentSourceType sourceType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrects an enrollment that was recorded wrongly — the grade the child was put in, the
        /// date it took effect, or where it came from (doc/Modules/10 §5).
        /// <para>
        /// Not a promotion and not a transfer. Until this existed the only answer to "the clerk
        /// enrolled him in grade 3 and he is in grade 4" was a second enrollment, which BR-GLB-024
        /// refuses outright — so the mistake stayed on the record and every register, mark sheet and
        /// fee schedule that reads the grade from it read the wrong one.
        /// </para>
        /// <para>
        /// The target profile must belong to the <b>same academic year</b>
        /// (<see cref="Common.Exceptions.EnrollmentYearChangeException"/>). Moving a child between
        /// years is the rollover's job (BR-GLB-023): the enrollment is what every year-scoped row
        /// hangs off, so re-pointing it at another year would silently re-file this year's
        /// attendance, marks and charges under a year they did not happen in.
        /// </para>
        /// <para>
        /// It is also refused while the student holds a section seat
        /// (<see cref="Common.Exceptions.EnrollmentSeatedException"/>). A section belongs to one
        /// grade-year, so correcting the grade underneath a seated child would leave them on the
        /// register of a section their grade no longer contains. Give up the seat, correct the
        /// grade, seat them again — three visible steps rather than one that quietly does two of
        /// them.
        /// </para>
        /// <para>
        /// The change itself is captured by the audit tier on <c>Enrollment</c> (T2, field-level),
        /// so what the grade was before is recoverable without this method writing anything extra.
        /// </para>
        /// </summary>
        Task<Enrollment> CorrectEnrollmentAsync(
            int enrollmentId, int gradeYearProfileId, DateTime enrollmentDate, EnrollmentSourceType sourceType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an enrollment that should never have been written, together with the section
        /// memberships that were part of the same placement.
        /// <para>
        /// BR-GLB-005 forbids deleting records, and this does not contradict it: the rule protects
        /// <i>history</i>, and an enrollment nothing has happened against is not history but a
        /// typing mistake. The line between the two is drawn by
        /// <see cref="Common.Guards.IUsageInspector{T}"/> rather than by judgement — one attendance
        /// day, one mark, one charge, one issued certificate against this enrollment and it is a
        /// record of something, at which point it is withdrawn (an exit date and a status) and
        /// never removed. The same distinction <c>DeleteStudentAsync</c> and
        /// <c>ISectionAdmin.DeleteSectionAsync</c> already draw.
        /// </para>
        /// <para>
        /// Throws <see cref="Common.Guards.RecordInUseException"/> naming what stands in the way,
        /// so the screen can say which module to clear first — and can hide the button before the
        /// operator ever presses it.
        /// </para>
        /// <para>
        /// <paramref name="reason"/> is mandatory (BR-GLB-032) and is written as an explicit
        /// <c>AuditAction.Delete</c> entry in the same transaction, carrying what the row said. The
        /// declarative captor cannot do it: it diffs added and modified entries, so a removed row
        /// would otherwise leave the audit trail with no sign it ever existed.
        /// Throws <see cref="Common.Guards.MissingRemovalReasonException"/> when it is blank.
        /// </para>
        /// </summary>
        Task RemoveEnrollmentAsync(int enrollmentId, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Hard-deletes a student record together with its guardian links, emergency contacts,
        /// enrollments and section memberships. Refused (InvalidOperationException) when the student
        /// already has history in other modules (attendance, charges, certificates) or other records
        /// still reference it. An admission application registered into this student is reverted to
        /// Approved (seat kept, no student) so the admission trail stays consistent.
        /// </summary>
        Task DeleteStudentAsync(int studentId, CancellationToken cancellationToken = default);
    }
}
