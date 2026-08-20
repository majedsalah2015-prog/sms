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
        /// Hard-deletes a student record together with its guardian links, emergency contacts,
        /// enrollments and section memberships. Refused (InvalidOperationException) when the student
        /// already has history in other modules (attendance, charges, certificates) or other records
        /// still reference it. An admission application registered into this student is reverted to
        /// Approved (seat kept, no student) so the admission trail stays consistent.
        /// </summary>
        Task DeleteStudentAsync(int studentId, CancellationToken cancellationToken = default);
    }
}
