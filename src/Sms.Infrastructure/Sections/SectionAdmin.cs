using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Grades;
using Sms.Application.Sections;
using Sms.Domain.Grades;
using Sms.Domain.Sections;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Sections
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class SectionAdmin : ISectionAdmin
    {
        private readonly AppDbContext _db;

        public SectionAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Section> DefineSectionAsync(
            int gradeYearProfileId, string nameAr, string nameEn, int capacity, GenderPolicy genderPolicy,
            int? defaultClassroomId = null, CancellationToken cancellationToken = default)
        {
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);

            if (!SectionCapacityGuard.WithinGradePlan(capacity, profile.TargetSectionSize))
            {
                throw new SectionCapacityPlanExceededException(capacity, profile.TargetSectionSize);
            }

            if (!GenderPolicyNarrowing.IsValidNarrowing(profile.GenderPolicy, genderPolicy))
            {
                throw new InvalidSectionGenderPolicyException(profile.GenderPolicy, genderPolicy);
            }

            var nameTaken = await _db.Sections.AnyAsync(
                s => s.GradeYearProfileId == gradeYearProfileId && s.NameEn == nameEn, cancellationToken);
            if (nameTaken)
            {
                throw new DuplicateSectionNameException(nameEn);
            }

            var section = new Section
            {
                AcademicYearId = profile.AcademicYearId,
                GradeYearProfileId = gradeYearProfileId,
                NameAr = nameAr,
                NameEn = nameEn,
                Capacity = capacity,
                GenderPolicy = genderPolicy,
                DefaultClassroomId = defaultClassroomId,
            };
            _db.Sections.Add(section);

            await _db.SaveChangesAsync(cancellationToken);
            return section;
        }

        public async Task<Section> UpdateSectionAsync(
            int sectionId, string nameAr, string nameEn, int capacity, GenderPolicy genderPolicy,
            int? defaultClassroomId = null, CancellationToken cancellationToken = default)
        {
            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == section.GradeYearProfileId, cancellationToken);

            if (!SectionCapacityGuard.WithinGradePlan(capacity, profile.TargetSectionSize))
            {
                throw new SectionCapacityPlanExceededException(capacity, profile.TargetSectionSize);
            }

            if (!GenderPolicyNarrowing.IsValidNarrowing(profile.GenderPolicy, genderPolicy))
            {
                throw new InvalidSectionGenderPolicyException(profile.GenderPolicy, genderPolicy);
            }

            var currentCount = await _db.SectionMemberships.CountAsync(m => m.SectionId == sectionId && m.EffectiveToUtc == null, cancellationToken);
            if (capacity < currentCount)
            {
                throw new SectionInUseException(sectionId, $"capacity {capacity} is below the {currentCount} currently assigned student(s)");
            }

            var nameTaken = await _db.Sections.AnyAsync(
                s => s.GradeYearProfileId == section.GradeYearProfileId && s.NameEn == nameEn && s.Id != sectionId, cancellationToken);
            if (nameTaken)
            {
                throw new DuplicateSectionNameException(nameEn);
            }

            section.NameAr = nameAr;
            section.NameEn = nameEn;
            section.Capacity = capacity;
            section.GenderPolicy = genderPolicy;
            section.DefaultClassroomId = defaultClassroomId;
            await _db.SaveChangesAsync(cancellationToken);
            return section;
        }

        public async Task DeleteSectionAsync(int sectionId, CancellationToken cancellationToken = default)
        {
            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);
            var memberships = await _db.SectionMemberships.CountAsync(m => m.SectionId == sectionId, cancellationToken);
            if (memberships > 0)
            {
                throw new SectionInUseException(sectionId, $"{memberships} membership record(s) exist — close the section instead (BR-SCN-007)");
            }

            var homerooms = await _db.HomeroomAssignments.CountAsync(h => h.SectionId == sectionId, cancellationToken);
            if (homerooms > 0)
            {
                throw new SectionInUseException(sectionId, $"{homerooms} homeroom assignment(s) exist — close the section instead");
            }

            _db.Sections.Remove(section);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new SectionInUseException(sectionId, "other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ")");
            }
        }

        public async Task<HomeroomAssignment> AssignHomeroomTeacherAsync(
            int sectionId, int teacherUserId, DateTime effectiveFromUtc, CancellationToken cancellationToken = default)
        {
            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);

            var current = await _db.HomeroomAssignments.SingleOrDefaultAsync(
                h => h.SectionId == sectionId && h.EffectiveToUtc == null, cancellationToken);
            if (current != null)
            {
                current.EffectiveToUtc = effectiveFromUtc;
            }

            var assignment = new HomeroomAssignment
            {
                AcademicYearId = section.AcademicYearId,
                SectionId = sectionId,
                TeacherUserId = teacherUserId,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.HomeroomAssignments.Add(assignment);

            await _db.SaveChangesAsync(cancellationToken);
            return assignment;
        }

        public async Task<SectionMembership> AssignMembershipAsync(
            int sectionId, int enrollmentId, DateTime effectiveFromUtc, CancellationToken cancellationToken = default)
        {
            var section = await EnsureCapacityAsync(sectionId, cancellationToken);

            var membership = new SectionMembership
            {
                AcademicYearId = section.AcademicYearId,
                SectionId = sectionId,
                EnrollmentId = enrollmentId,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.SectionMemberships.Add(membership);

            await _db.SaveChangesAsync(cancellationToken);
            return membership;
        }

        public async Task<SectionMembership> TransferMembershipAsync(
            int enrollmentId, int targetSectionId, string transferReasonCode, DateTime effectiveDate, CancellationToken cancellationToken = default)
        {
            var targetSection = await EnsureCapacityAsync(targetSectionId, cancellationToken);

            var currentMembership = await _db.SectionMemberships.SingleOrDefaultAsync(
                m => m.EnrollmentId == enrollmentId && m.EffectiveToUtc == null, cancellationToken);
            if (currentMembership != null)
            {
                currentMembership.EffectiveToUtc = effectiveDate;
            }

            var newMembership = new SectionMembership
            {
                AcademicYearId = targetSection.AcademicYearId,
                SectionId = targetSectionId,
                EnrollmentId = enrollmentId,
                EffectiveFromUtc = effectiveDate,
                TransferReasonCode = transferReasonCode,
            };
            _db.SectionMemberships.Add(newMembership);

            await _db.SaveChangesAsync(cancellationToken);
            return newMembership;
        }

        public async Task CloseSectionAsync(int sectionId, CancellationToken cancellationToken = default)
        {
            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);
            var memberCount = await _db.SectionMemberships.CountAsync(
                m => m.SectionId == sectionId && m.EffectiveToUtc == null, cancellationToken);
            if (memberCount > 0)
            {
                throw new SectionCloseWithMembersException(sectionId, memberCount);
            }

            section.Status = SectionStatus.Closed;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<Section> EnsureCapacityAsync(int sectionId, CancellationToken cancellationToken)
        {
            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);
            var currentCount = await _db.SectionMemberships.CountAsync(
                m => m.SectionId == sectionId && m.EffectiveToUtc == null, cancellationToken);
            if (!SectionCapacityGuard.CanAssign(currentCount, section.Capacity))
            {
                throw new SectionFullException(sectionId);
            }

            return section;
        }
    }
}
