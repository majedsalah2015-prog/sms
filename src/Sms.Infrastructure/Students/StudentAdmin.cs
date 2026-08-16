using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Numbering;
using Sms.Application.Students;
using Sms.Domain.Common;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Students
{
    /// <summary>
    /// Standalone admin operations — save themselves, no larger transaction
    /// to ride. RegisterStudentAsync composes with E-006's INumberIssuer:
    /// the number is issued (mutating the ambient SeriesState) and this
    /// method's own SaveChangesAsync commits the number and the new Student
    /// row together (BR-NUM-003).
    /// </summary>
    public class StudentAdmin : IStudentAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;

        public StudentAdmin(AppDbContext db, INumberIssuer numberIssuer)
        {
            _db = db;
            _numberIssuer = numberIssuer;
        }

        public async Task<Student> RegisterStudentAsync(
            string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default)
        {
            var studentNo = await _numberIssuer.IssueAsync("STU", cancellationToken);

            var student = new Student
            {
                StudentNo = studentNo,
                FirstNameAr = firstNameAr,
                FatherNameAr = fatherNameAr,
                GrandfatherNameAr = grandfatherNameAr,
                FamilyNameAr = familyNameAr,
                FirstNameEn = firstNameEn,
                FatherNameEn = fatherNameEn,
                GrandfatherNameEn = grandfatherNameEn,
                FamilyNameEn = familyNameEn,
                Gender = gender,
                DateOfBirth = dateOfBirth,
                NationalityLookupId = nationalityLookupId,
                PrimaryIdTypeLookupId = primaryIdTypeLookupId,
                PrimaryIdNo = primaryIdNo,
                PrimaryIdExpiry = primaryIdExpiry,
            };
            _db.Students.Add(student);

            await _db.SaveChangesAsync(cancellationToken);
            return student;
        }

        public async Task ChangeStatusAsync(int studentId, StudentStatus newStatus, CancellationToken cancellationToken = default)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);
            if (!StudentStatusTransitions.CanTransition(student.Status, newStatus))
            {
                throw new InvalidStudentStatusTransitionException(student.Status, newStatus);
            }

            student.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<StudentGuardianLink> LinkGuardianAsync(
            int studentId, int parentId, int relationshipLookupId, bool isPrimaryContact, bool isFinanciallyResponsible,
            bool isPickupAuthorized, bool isPortalVisible, DateTime effectiveFromUtc, int? guardianshipDocAttachmentId = null,
            CancellationToken cancellationToken = default)
        {
            var link = new StudentGuardianLink
            {
                StudentId = studentId,
                ParentId = parentId,
                RelationshipLookupId = relationshipLookupId,
                IsPrimaryContact = isPrimaryContact,
                IsFinanciallyResponsible = isFinanciallyResponsible,
                IsPickupAuthorized = isPickupAuthorized,
                IsPortalVisible = isPortalVisible,
                GuardianshipDocAttachmentId = guardianshipDocAttachmentId,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.StudentGuardianLinks.Add(link);

            await _db.SaveChangesAsync(cancellationToken);
            return link;
        }

        public async Task UnlinkGuardianAsync(int linkId, DateTime effectiveToUtc, CancellationToken cancellationToken = default)
        {
            var link = await _db.StudentGuardianLinks.SingleAsync(l => l.Id == linkId, cancellationToken);

            if (link.IsFinanciallyResponsible)
            {
                var otherResponsibleFlags = await _db.StudentGuardianLinks
                    .Where(l => l.StudentId == link.StudentId && l.Id != linkId && l.EffectiveToUtc == null)
                    .Select(l => l.IsFinanciallyResponsible)
                    .ToListAsync(cancellationToken);

                if (!FinancialResponsibilityGuard.HasAtLeastOneResponsible(otherResponsibleFlags))
                {
                    throw new LastFinanciallyResponsibleGuardianException(link.StudentId);
                }
            }

            link.EffectiveToUtc = effectiveToUtc;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<EmergencyContact> AddEmergencyContactAsync(
            int studentId, string nameAr, string nameEn, string phone, bool isPickupAuthorized,
            int? relationshipLookupId = null, CancellationToken cancellationToken = default)
        {
            var contact = new EmergencyContact
            {
                StudentId = studentId,
                NameAr = nameAr,
                NameEn = nameEn,
                Phone = phone,
                IsPickupAuthorized = isPickupAuthorized,
                RelationshipLookupId = relationshipLookupId,
            };
            _db.EmergencyContacts.Add(contact);

            await _db.SaveChangesAsync(cancellationToken);
            return contact;
        }

        public async Task<Enrollment> EnrollAsync(
            int studentId, int gradeYearProfileId, DateTime enrollmentDate, EnrollmentSourceType sourceType, CancellationToken cancellationToken = default)
        {
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);

            var activeExists = await _db.Enrollments.AnyAsync(
                e => e.StudentId == studentId && e.AcademicYearId == profile.AcademicYearId && e.Status == EnrollmentStatus.Active, cancellationToken);
            if (activeExists)
            {
                throw new DuplicateEnrollmentException(studentId, profile.AcademicYearId);
            }

            var enrollment = new Enrollment
            {
                AcademicYearId = profile.AcademicYearId,
                StudentId = studentId,
                GradeYearProfileId = gradeYearProfileId,
                EnrollmentDate = enrollmentDate,
                SourceType = sourceType,
            };
            _db.Enrollments.Add(enrollment);

            await _db.SaveChangesAsync(cancellationToken);
            return enrollment;
        }
    }
}
