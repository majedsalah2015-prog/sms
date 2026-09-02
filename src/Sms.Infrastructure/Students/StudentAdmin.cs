using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.Application.Numbering;
using Sms.Application.Students;
using Sms.Domain.Audit;
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
        private readonly IAuditEventWriter _auditEvents;
        private readonly IUsageInspector<Enrollment> _enrollmentUsage;

        /// <param name="auditEvents">
        /// Removing an enrollment is the one write here that <c>AuditCaptor</c> cannot see:
        /// it diffs <c>Added</c> and <c>Modified</c> entries, so a deleted row would leave no
        /// trace at all. <see cref="RemoveEnrollmentAsync"/> logs it explicitly.
        /// </param>
        /// <param name="enrollmentUsage">
        /// What would break if an enrollment went away. Optional because it is a pure query over the
        /// same context with no state of its own; the container passes the registered one, and a
        /// caller that does not care gets the same object built here rather than a null to guard.
        /// </param>
        public StudentAdmin(
            AppDbContext db, INumberIssuer numberIssuer, IAuditEventWriter auditEvents,
            IUsageInspector<Enrollment>? enrollmentUsage = null)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _auditEvents = auditEvents;
            _enrollmentUsage = enrollmentUsage ?? new EnrollmentUsageInspector(db);
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

        public async Task<Student> UpdateStudentAsync(
            int studentId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId,
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null, DateTime? primaryIdExpiry = null,
            CancellationToken cancellationToken = default)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);
            student.FirstNameAr = firstNameAr; student.FatherNameAr = fatherNameAr; student.GrandfatherNameAr = grandfatherNameAr; student.FamilyNameAr = familyNameAr;
            student.FirstNameEn = firstNameEn; student.FatherNameEn = fatherNameEn; student.GrandfatherNameEn = grandfatherNameEn; student.FamilyNameEn = familyNameEn;
            student.Gender = gender; student.DateOfBirth = dateOfBirth; student.NationalityLookupId = nationalityLookupId;
            student.PrimaryIdTypeLookupId = primaryIdTypeLookupId; student.PrimaryIdNo = primaryIdNo; student.PrimaryIdExpiry = primaryIdExpiry;
            await _db.SaveChangesAsync(cancellationToken);
            return student;
        }

        public async Task<Student> UpdateSocialProfileAsync(
            int studentId,
            Religion? religion,
            ResidencyStatus? residencyStatus, FinancialStatus? financialStatus, string? rationCardNo,
            string? placeOfBirth, int? familySize, int? birthOrder, int? siblingCount, string? mobile,
            CancellationToken cancellationToken = default)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);

            // Blanks are stored as null, not as "". A social profile is read as "recorded or not" —
            // an empty ration-card box means the school does not have the number, and "" would make
            // that indistinguishable from a number of zero length in every later query.
            student.Religion = religion;
            student.ResidencyStatus = residencyStatus;
            student.FinancialStatus = financialStatus;
            student.RationCardNo = Blank(rationCardNo);

            student.PlaceOfBirth = Blank(placeOfBirth);
            student.FamilySize = familySize;
            student.BirthOrder = birthOrder;
            student.SiblingCount = siblingCount;
            student.Mobile = Blank(mobile);

            await _db.SaveChangesAsync(cancellationToken);
            return student;
        }

        public async Task SetResidenceAsync(int studentId, int? residenceAreaId, int? neighbourhoodId, CancellationToken cancellationToken = default)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);

            // A quarter without its locality is not a place, and a quarter belonging to a different
            // locality is a worse record than none: neither is stored. Same two refusals the parent
            // register makes, sharing its exception so the boundary translates both from one arm.
            if (neighbourhoodId is int hoodId)
            {
                if (residenceAreaId is not int areaId)
                {
                    throw new InvalidResidenceSelectionException(ResidenceSelectionFault.QuarterWithoutLocality);
                }

                var belongs = await _db.Neighbourhoods.AnyAsync(n => n.Id == hoodId && n.ResidenceAreaId == areaId, cancellationToken);
                if (!belongs)
                {
                    throw new InvalidResidenceSelectionException(ResidenceSelectionFault.QuarterOutsideLocality);
                }
            }

            student.ResidenceAreaId = residenceAreaId;

            // Clearing the locality clears the quarter under it rather than orphaning it: a quarter
            // with nothing above it is the very record the refusal above exists to prevent, and it
            // must not be reachable by the back door of blanking one box.
            student.NeighbourhoodId = residenceAreaId == null ? null : neighbourhoodId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        public async Task<Enrollment> CorrectEnrollmentAsync(
            int enrollmentId, int gradeYearProfileId, DateTime enrollmentDate, EnrollmentSourceType sourceType,
            CancellationToken cancellationToken = default)
        {
            var enrollment = await _db.Enrollments.SingleAsync(e => e.Id == enrollmentId, cancellationToken);

            // Past the soft-active filter on purpose. A grade retired since the mistake was made
            // still has to be nameable as the thing being corrected *away from*, and the target is
            // re-validated below rather than trusted from a picker.
            var profile = await _db.GradeYearProfiles.IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.Id == gradeYearProfileId && p.SchoolId == enrollment.SchoolId, cancellationToken);
            if (profile == null)
            {
                throw new InvalidOperationException($"Grade-year profile {gradeYearProfileId} does not exist in this school.");
            }

            if (profile.AcademicYearId != enrollment.AcademicYearId)
            {
                throw new EnrollmentYearChangeException(enrollmentId, enrollment.AcademicYearId, profile.AcademicYearId);
            }

            // The seat, not the grade, is what makes this refuse: a section belongs to one
            // grade-year, so a corrected grade would leave the child on a register that no longer
            // contains their grade. Checked before anything is written.
            var seat = await _db.SectionMemberships.AsNoTracking()
                .SingleOrDefaultAsync(m => m.EnrollmentId == enrollmentId && m.EffectiveToUtc == null, cancellationToken);
            if (seat != null && enrollment.GradeYearProfileId != gradeYearProfileId)
            {
                var section = await _db.Sections.AsNoTracking().SingleAsync(s => s.Id == seat.SectionId, cancellationToken);
                throw new EnrollmentSeatedException(enrollmentId, section.NameEn, section.NameAr);
            }

            enrollment.GradeYearProfileId = gradeYearProfileId;
            enrollment.EnrollmentDate = enrollmentDate;
            enrollment.SourceType = sourceType;

            // No audit call of its own: Enrollment is [Audited(AuditTier.T2)], so the field-level
            // before/after is captured inside this SaveChanges, in the same transaction as the change.
            await _db.SaveChangesAsync(cancellationToken);
            return enrollment;
        }

        public async Task RemoveEnrollmentAsync(int enrollmentId, string reason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new MissingRemovalReasonException(nameof(Enrollment));
            }

            var enrollment = await _db.Enrollments.SingleAsync(e => e.Id == enrollmentId, cancellationToken);

            var usage = await _enrollmentUsage.InspectAsync(enrollmentId, cancellationToken);
            if (usage.IsInUse)
            {
                throw new RecordInUseException(usage);
            }

            // The memberships are the placement itself, not something recorded against it, so they
            // go with it — closed ones included. The guard above has already established that
            // nothing was ever taken, marked or charged while the child sat in them.
            _db.SectionMemberships.RemoveRange(
                await _db.SectionMemberships.Where(m => m.EnrollmentId == enrollmentId).ToListAsync(cancellationToken));
            _db.Enrollments.Remove(enrollment);

            // Written before the save, so the entry and the removal are one transaction (BR-AUD-003)
            // — and written at all because the captor never sees a delete. The business key carries
            // what the row said, since after this commits there is nothing left to join back to.
            _auditEvents.Log(
                AuditAction.Delete,
                nameof(Enrollment),
                enrollmentId,
                businessKey: FormattableString.Invariant(
                    $"student {enrollment.StudentId}, grade-year profile {enrollment.GradeYearProfileId}, year {enrollment.AcademicYearId}"),
                reason: reason.Trim());

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // The inspector names every reference this build knows about; a module added later
                // that points at an enrollment without being listed there arrives here instead, as
                // a foreign key. Refusing is right either way — the message is simply less useful.
                throw new InvalidOperationException(
                    "Enrollment cannot be removed: other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ").");
            }
        }

        public async Task DeleteStudentAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);

            var enrollments = await _db.Enrollments.Where(e => e.StudentId == studentId).ToListAsync(cancellationToken);
            var enrollmentIds = enrollments.Select(e => e.Id).ToList();

            // History in other modules blocks deletion — those records must not lose their student.
            if (await _db.AttendanceDays.AnyAsync(a => enrollmentIds.Contains(a.EnrollmentId), cancellationToken))
                throw new InvalidOperationException("Student has attendance records and cannot be deleted.");
            if (await _db.Charges.AnyAsync(c => c.StudentId == studentId, cancellationToken))
                throw new InvalidOperationException("Student has fee charges and cannot be deleted.");
            if (await _db.CertificateIssues.AnyAsync(c => c.StudentId == studentId, cancellationToken))
                throw new InvalidOperationException("Student has issued certificates and cannot be deleted.");

            foreach (var application in await _db.Applications.Where(a => a.RegisteredStudentId == studentId).ToListAsync(cancellationToken))
            {
                application.RegisteredStudentId = null;
                application.Status = Sms.Domain.Admissions.ApplicationStatus.Approved;
            }

            _db.SectionMemberships.RemoveRange(await _db.SectionMemberships.Where(m => enrollmentIds.Contains(m.EnrollmentId)).ToListAsync(cancellationToken));
            _db.Enrollments.RemoveRange(enrollments);
            _db.StudentGuardianLinks.RemoveRange(await _db.StudentGuardianLinks.Where(l => l.StudentId == studentId).ToListAsync(cancellationToken));
            _db.EmergencyContacts.RemoveRange(await _db.EmergencyContacts.Where(c => c.StudentId == studentId).ToListAsync(cancellationToken));
            _db.Students.Remove(student);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Student cannot be deleted: other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ").");
            }
        }
    }
}
