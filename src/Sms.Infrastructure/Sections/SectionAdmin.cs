using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Grades;
using Sms.Application.Rollover;
using Sms.Application.Sections;
using Sms.Domain.Common;
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

        public async Task<IReadOnlyList<Section>> DefineSectionsAsync(
            int gradeYearProfileId, int count, int capacity, GenderPolicy genderPolicy,
            CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return Array.Empty<Section>();
            }

            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);

            // Checked once for the batch rather than per section: capacity and gender
            // are the same for all of them, so a count that breaks the plan on the
            // third must refuse all four instead of leaving two behind.
            if (!SectionCapacityGuard.WithinGradePlan(capacity, profile.TargetSectionSize))
            {
                throw new SectionCapacityPlanExceededException(capacity, profile.TargetSectionSize);
            }

            if (!GenderPolicyNarrowing.IsValidNarrowing(profile.GenderPolicy, genderPolicy))
            {
                throw new InvalidSectionGenderPolicyException(profile.GenderPolicy, genderPolicy);
            }

            // IgnoreQueryFilters on the grade: a profile whose grade the school retired
            // still has sections to add to mid-year, and the name pattern is read off
            // the grade's own short name.
            var grade = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(g => g.Id == profile.GradeLevelId, cancellationToken);

            // Every section in the grade, closed ones included: their names are still
            // taken, which is exactly what the sequence has to continue past.
            var existing = (await _db.Sections.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && s.GradeYearProfileId == gradeYearProfileId)
                .OrderBy(s => s.Id)
                .Select(s => new { s.NameAr, s.NameEn })
                .ToListAsync(cancellationToken))
                .Select(s => new SectionNameSequence.ExistingName(s.NameAr, s.NameEn))
                .ToList();

            var proposed = SectionNameSequence.Next(grade.Name.NameAr, grade.Name.NameEn, existing, count);
            var taken = existing.Select(e => e.NameEn).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in proposed.Where(n => taken.Contains(n.NameEn)))
            {
                // The sequence continues past the highest, so this should be
                // unreachable — but a name collision is the one failure that would
                // otherwise surface as a unique-index crash halfway through the batch.
                throw new DuplicateSectionNameException(name.NameEn);
            }

            var sections = proposed.Select(name => new Section
            {
                AcademicYearId = profile.AcademicYearId,
                GradeYearProfileId = gradeYearProfileId,
                NameAr = name.NameAr,
                NameEn = name.NameEn,
                Capacity = capacity,
                GenderPolicy = genderPolicy,
            }).ToList();

            _db.Sections.AddRange(sections);
            await _db.SaveChangesAsync(cancellationToken);
            return sections;
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
                throw new SectionInUseException(sectionId, SectionInUseReason.CapacityBelowAssigned, capacity, currentCount);
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
                throw new SectionInUseException(sectionId, SectionInUseReason.HasHistory, existing: memberships);
            }

            var homerooms = await _db.HomeroomAssignments.CountAsync(h => h.SectionId == sectionId, cancellationToken);
            if (homerooms > 0)
            {
                throw new SectionInUseException(sectionId, SectionInUseReason.HasHistory, existing: homerooms);
            }

            _db.Sections.Remove(section);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new SectionInUseException(sectionId, SectionInUseReason.ReferencedElsewhere, ex);
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
            await EnsureGenderAsync(section, enrollmentId, cancellationToken);

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
            await EnsureGenderAsync(targetSection, enrollmentId, cancellationToken);

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

        public async Task<SectionMembership?> EndMembershipAsync(
            int enrollmentId, string reasonCode, DateTime effectiveDate, CancellationToken cancellationToken = default)
        {
            var current = await _db.SectionMemberships.SingleOrDefaultAsync(
                m => m.EnrollmentId == enrollmentId && m.EffectiveToUtc == null, cancellationToken);
            if (current == null)
            {
                return null;
            }

            current.EffectiveToUtc = effectiveDate;

            // The reason lands on the row being closed, not on a new one — there is no new one. On a
            // transfer the code rides the membership that opens, because it answers "why is he here
            // now"; here it answers "why did he leave", which is a property of the seat he vacated.
            current.TransferReasonCode = reasonCode;

            await _db.SaveChangesAsync(cancellationToken);
            return current;
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

        public async Task<PlacementOutcome> ApplyDistributionAsync(
            IReadOnlyDictionary<int, int> placements, string transferReasonCode, DateTime effectiveDate,
            CancellationToken cancellationToken = default)
        {
            if (placements.Count == 0)
            {
                return new PlacementOutcome(0, 0);
            }

            var outcome = await StagePlacementsAsync(placements, transferReasonCode, effectiveDate, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return outcome;
        }

        public async Task<int> MergeAndCloseSectionAsync(
            int sectionId, IReadOnlyDictionary<int, int> placements, string transferReasonCode, DateTime effectiveDate,
            CancellationToken cancellationToken = default)
        {
            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);

            var members = await _db.SectionMemberships.AsNoTracking()
                .Where(m => m.SectionId == sectionId && m.EffectiveToUtc == null)
                .Select(m => m.EnrollmentId).ToListAsync(cancellationToken);

            // Every member has to be given somewhere to go, and nobody may be sent back
            // in. Either omission would end with the section closed and a student still
            // recorded in it — a membership row pointing at a class that no longer runs.
            var unplaced = members.Count(id => !placements.ContainsKey(id) || placements[id] == sectionId);
            if (unplaced > 0)
            {
                throw new SectionCloseWithMembersException(sectionId, unplaced);
            }

            if (placements.Count > 0)
            {
                await StagePlacementsAsync(placements, transferReasonCode, effectiveDate, cancellationToken);
            }

            var homeroom = await _db.HomeroomAssignments
                .SingleOrDefaultAsync(h => h.SectionId == sectionId && h.EffectiveToUtc == null, cancellationToken);
            if (homeroom != null)
            {
                homeroom.EffectiveToUtc = effectiveDate;
            }

            section.Status = SectionStatus.Closed;
            await _db.SaveChangesAsync(cancellationToken);
            return members.Count;
        }

        /// <summary>
        /// Validates a whole layout and stages it, without saving — so a caller that
        /// has more to do in the same transaction (closing the section the students
        /// came out of) commits all of it or none.
        /// </summary>
        private async Task<PlacementOutcome> StagePlacementsAsync(
            IReadOnlyDictionary<int, int> placements, string transferReasonCode, DateTime effectiveDate,
            CancellationToken cancellationToken)
        {
            var enrollmentIds = placements.Keys.ToList();
            var sectionIds = placements.Values.Distinct().ToList();

            var sections = await _db.Sections.Where(s => sectionIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
            var missing = sectionIds.FirstOrDefault(id => !sections.ContainsKey(id));
            if (missing != 0)
            {
                throw new SectionGradeMismatchException(missing, 0);
            }

            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => enrollmentIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);
            var studentIds = enrollments.Values.Select(e => e.StudentId).Distinct().ToList();
            var genders = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => studentIds.Contains(s.Id) && s.SchoolId == _db.CurrentSchoolId)
                .ToDictionaryAsync(s => s.Id, s => s.Gender, cancellationToken);

            var current = await _db.SectionMemberships
                .Where(m => enrollmentIds.Contains(m.EnrollmentId) && m.EffectiveToUtc == null)
                .ToListAsync(cancellationToken);
            var currentBy = current.ToDictionary(m => m.EnrollmentId);

            // A section keeps the students nobody is moving. Counting only the batch
            // would let a layout drop three children into a section that already holds
            // every seat it has.
            var stayingCounts = await _db.SectionMemberships.AsNoTracking()
                .Where(m => sectionIds.Contains(m.SectionId) && m.EffectiveToUtc == null && !enrollmentIds.Contains(m.EnrollmentId))
                .GroupBy(m => m.SectionId)
                .Select(g => new { g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N, cancellationToken);

            foreach (var (sectionId, section) in sections.OrderBy(s => s.Key))
            {
                var landing = placements.Count(p => p.Value == sectionId)
                    + (stayingCounts.TryGetValue(sectionId, out var staying) ? staying : 0);
                if (landing > section.Capacity)
                {
                    throw new SectionFullException(sectionId);
                }
            }

            foreach (var (enrollmentId, sectionId) in placements.OrderBy(p => p.Key))
            {
                if (!enrollments.TryGetValue(enrollmentId, out var enrollment))
                {
                    throw new SectionGradeMismatchException(sectionId, 0);
                }

                var section = sections[sectionId];
                if (section.GradeYearProfileId != enrollment.GradeYearProfileId)
                {
                    throw new SectionGradeMismatchException(sectionId, enrollment.GradeYearProfileId);
                }

                if (genders.TryGetValue(enrollment.StudentId, out var gender)
                    && !SectionDistributor.IsGenderCompatible(section.GenderPolicy, gender))
                {
                    throw new SectionGenderMismatchException(sectionId, enrollment.StudentId);
                }
            }

            var seated = 0;
            var transferred = 0;
            foreach (var (enrollmentId, sectionId) in placements.OrderBy(p => p.Key))
            {
                currentBy.TryGetValue(enrollmentId, out var existing);
                if (existing != null && existing.SectionId == sectionId)
                {
                    continue;
                }

                if (existing != null)
                {
                    existing.EffectiveToUtc = effectiveDate;
                }

                _db.SectionMemberships.Add(new SectionMembership
                {
                    AcademicYearId = sections[sectionId].AcademicYearId,
                    SectionId = sectionId,
                    EnrollmentId = enrollmentId,
                    EffectiveFromUtc = effectiveDate,
                    // A first seat is not a transfer and carries no reason: BR-SCN-005's
                    // reason code answers "why was this child moved", and there is no
                    // answer to give when they were not.
                    TransferReasonCode = existing == null ? null : transferReasonCode,
                });

                if (existing == null)
                {
                    seated++;
                }
                else
                {
                    transferred++;
                }
            }

            return new PlacementOutcome(seated, transferred);
        }

        /// <summary>
        /// BR-SCN-003: the section's gender policy has to admit the student. This was
        /// checked when a section was defined (its policy must narrow the grade's) but
        /// never when a student was put in one, so a girl could be assigned to a boys'
        /// section from the roster screen.
        /// </summary>
        private async Task EnsureGenderAsync(Section section, int enrollmentId, CancellationToken cancellationToken)
        {
            var studentId = await _db.Enrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId).Select(e => (int?)e.StudentId).SingleOrDefaultAsync(cancellationToken);
            if (studentId == null)
            {
                return;
            }

            var gender = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == studentId.Value && s.SchoolId == _db.CurrentSchoolId)
                .Select(s => (Gender?)s.Gender).SingleOrDefaultAsync(cancellationToken);

            if (gender != null && !SectionDistributor.IsGenderCompatible(section.GenderPolicy, gender.Value))
            {
                throw new SectionGenderMismatchException(section.Id, studentId.Value);
            }
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
