using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Grades;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Grades
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class GradeStructureAdmin : IGradeStructureAdmin
    {
        private readonly AppDbContext _db;

        public GradeStructureAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Stage> DefineStageAsync(
            string nameAr, string nameEn, int sequenceOrder, GenderPolicy defaultGenderPolicy, CancellationToken cancellationToken = default)
        {
            var stage = new Stage
            {
                Name = new LocalizedName(nameAr, nameEn),
                SequenceOrder = sequenceOrder,
                DefaultGenderPolicy = defaultGenderPolicy,
            };
            _db.Stages.Add(stage);

            await _db.SaveChangesAsync(cancellationToken);
            return stage;
        }

        public async Task<GradeLevel> DefineGradeLevelAsync(
            int stageId, string code, string nameAr, string nameEn, int sequenceOrder,
            int? promotionTargetGradeLevelId, bool isGraduating, CancellationToken cancellationToken = default)
        {
            // Proactive check for a clear error on the expected path; the unique
            // index is the concurrency-safe backstop (same pattern as E-006/E-102).
            var exists = await _db.GradeLevels.AnyAsync(g => g.Code == code, cancellationToken);
            if (exists)
            {
                throw new DuplicateGradeCodeException(code);
            }

            var grade = new GradeLevel
            {
                StageId = stageId,
                Code = code,
                Name = new LocalizedName(nameAr, nameEn),
                SequenceOrder = sequenceOrder,
                PromotionTargetGradeLevelId = promotionTargetGradeLevelId,
                IsGraduating = isGraduating,
            };
            _db.GradeLevels.Add(grade);

            await _db.SaveChangesAsync(cancellationToken);
            return grade;
        }

        public async Task<Stage> UpdateStageAsync(int stageId, string nameAr, string nameEn, int sequenceOrder, GenderPolicy defaultGenderPolicy, CancellationToken cancellationToken = default)
        {
            var stage = await _db.Stages.SingleAsync(s => s.Id == stageId, cancellationToken);
            if (stage.DefaultGenderPolicy != defaultGenderPolicy)
            {
                // Every existing profile of this stage's grades must remain a valid narrowing (BR-GRD-004).
                var gradeIds = await _db.GradeLevels.Where(g => g.StageId == stageId).Select(g => g.Id).ToListAsync(cancellationToken);
                var policies = await _db.GradeYearProfiles.AsNoTracking().Where(p => gradeIds.Contains(p.GradeLevelId) && p.IsActive).Select(p => p.GenderPolicy).Distinct().ToListAsync(cancellationToken);
                var offending = policies.Where(p => !GenderPolicyNarrowing.IsValidNarrowing(defaultGenderPolicy, p)).ToList();
                if (offending.Count > 0)
                {
                    throw new InvalidGenderPolicyNarrowingException(defaultGenderPolicy, offending[0]);
                }
            }

            stage.Name = new LocalizedName(nameAr, nameEn);
            stage.SequenceOrder = sequenceOrder;
            stage.DefaultGenderPolicy = defaultGenderPolicy;
            await _db.SaveChangesAsync(cancellationToken);
            return stage;
        }

        public async Task DeactivateStageAsync(int stageId, CancellationToken cancellationToken = default)
        {
            var stage = await _db.Stages.SingleAsync(s => s.Id == stageId, cancellationToken);
            var grades = await _db.GradeLevels.CountAsync(g => g.StageId == stageId, cancellationToken);
            if (grades > 0)
            {
                throw new GradeStructureInUseException($"stage still has {grades} active grade level(s)");
            }

            stage.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<GradeLevel> UpdateGradeLevelAsync(int gradeLevelId, int stageId, string code, string nameAr, string nameEn, int sequenceOrder, CancellationToken cancellationToken = default)
        {
            var grade = await _db.GradeLevels.SingleAsync(g => g.Id == gradeLevelId, cancellationToken);
            if (await _db.GradeLevels.AnyAsync(g => g.Code == code && g.Id != gradeLevelId, cancellationToken))
            {
                throw new DuplicateGradeCodeException(code);
            }

            if (grade.StageId != stageId)
            {
                // Moving to another stage: its profiles must still narrow the new stage's default (BR-GRD-004).
                var stage = await _db.Stages.SingleAsync(s => s.Id == stageId, cancellationToken);
                var policies = await _db.GradeYearProfiles.AsNoTracking().Where(p => p.GradeLevelId == gradeLevelId && p.IsActive).Select(p => p.GenderPolicy).Distinct().ToListAsync(cancellationToken);
                var offending = policies.Where(p => !GenderPolicyNarrowing.IsValidNarrowing(stage.DefaultGenderPolicy, p)).ToList();
                if (offending.Count > 0)
                {
                    throw new InvalidGenderPolicyNarrowingException(stage.DefaultGenderPolicy, offending[0]);
                }
            }

            grade.StageId = stageId;
            grade.Code = code;
            grade.Name = new LocalizedName(nameAr, nameEn);
            grade.SequenceOrder = sequenceOrder;
            await _db.SaveChangesAsync(cancellationToken);
            return grade;
        }

        public async Task DeactivateGradeLevelAsync(int gradeLevelId, CancellationToken cancellationToken = default)
        {
            var grade = await _db.GradeLevels.SingleAsync(g => g.Id == gradeLevelId, cancellationToken);
            var profileIds = await _db.GradeYearProfiles.Where(p => p.GradeLevelId == gradeLevelId).Select(p => p.Id).ToListAsync(cancellationToken);

            var enrollments = await _db.Enrollments.CountAsync(e => profileIds.Contains(e.GradeYearProfileId), cancellationToken);
            if (enrollments > 0)
            {
                throw new GradeStructureInUseException($"{enrollments} enrollment(s) exist for this grade");
            }

            var sections = await _db.Sections.CountAsync(s => profileIds.Contains(s.GradeYearProfileId), cancellationToken);
            if (sections > 0)
            {
                throw new GradeStructureInUseException($"{sections} section(s) exist for this grade");
            }

            var feeders = await _db.GradeLevels.Where(g => g.PromotionTargetGradeLevelId == gradeLevelId).Select(g => g.Code).ToListAsync(cancellationToken);
            if (feeders.Count > 0)
            {
                throw new GradeStructureInUseException($"grade(s) {string.Join(", ", feeders)} promote into it — change their promotion path first (BR-GRD-002)");
            }

            foreach (var p in await _db.GradeYearProfiles.Where(p => profileIds.Contains(p.Id)).ToListAsync(cancellationToken))
            {
                p.IsActive = false;
            }

            grade.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveGradeYearProfileAsync(int gradeYearProfileId, CancellationToken cancellationToken = default)
        {
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);
            var enrollments = await _db.Enrollments.CountAsync(e => e.GradeYearProfileId == gradeYearProfileId, cancellationToken);
            if (enrollments > 0)
            {
                throw new GradeStructureInUseException($"{enrollments} enrollment(s) exist for this grade-year profile");
            }

            var sections = await _db.Sections.CountAsync(s => s.GradeYearProfileId == gradeYearProfileId, cancellationToken);
            if (sections > 0)
            {
                throw new GradeStructureInUseException($"{sections} section(s) exist for this grade-year profile");
            }

            profile.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetPromotionPathAsync(int gradeLevelId, int? promotionTargetGradeLevelId, bool isGraduating, CancellationToken cancellationToken = default)
        {
            var grade = await _db.GradeLevels.SingleAsync(g => g.Id == gradeLevelId, cancellationToken);
            if (promotionTargetGradeLevelId == gradeLevelId)
            {
                throw new PromotionPathCycleException();
            }

            var all = await _db.GradeLevels.AsNoTracking().ToListAsync(cancellationToken);
            var snapshot = all.Select(g => new GradeSnapshot(g.Id, g.Id == gradeLevelId ? promotionTargetGradeLevelId : g.PromotionTargetGradeLevelId, g.Id == gradeLevelId ? isGraduating : g.IsGraduating)).ToList();
            if (PromotionPathValidator.HasCycle(snapshot))
            {
                throw new PromotionPathCycleException();
            }

            grade.PromotionTargetGradeLevelId = promotionTargetGradeLevelId;
            grade.IsGraduating = isGraduating;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<GradeYearProfile> DefineGradeYearProfileAsync(
            int gradeLevelId, int academicYearId, GenderPolicy genderPolicy, int targetSections, int targetSectionSize,
            int? curriculumLookupValueId = null, decimal? minAgeAtCutoff = null, decimal? maxAgeAtCutoff = null,
            DateTime? ageCutoffDate = null, CancellationToken cancellationToken = default)
        {
            var grade = await _db.GradeLevels.SingleAsync(g => g.Id == gradeLevelId, cancellationToken);
            var stage = await _db.Stages.SingleAsync(s => s.Id == grade.StageId, cancellationToken);
            if (!GenderPolicyNarrowing.IsValidNarrowing(stage.DefaultGenderPolicy, genderPolicy))
            {
                throw new InvalidGenderPolicyNarrowingException(stage.DefaultGenderPolicy, genderPolicy);
            }

            var profile = await _db.GradeYearProfiles.SingleOrDefaultAsync(
                p => p.GradeLevelId == gradeLevelId && p.AcademicYearId == academicYearId, cancellationToken);
            if (profile == null)
            {
                profile = new GradeYearProfile { GradeLevelId = gradeLevelId, AcademicYearId = academicYearId };
                _db.GradeYearProfiles.Add(profile);
            }

            profile.GenderPolicy = genderPolicy;
            profile.TargetSections = targetSections;
            profile.TargetSectionSize = targetSectionSize;
            profile.CurriculumLookupValueId = curriculumLookupValueId;
            profile.MinAgeAtCutoff = minAgeAtCutoff;
            profile.MaxAgeAtCutoff = maxAgeAtCutoff;
            profile.AgeCutoffDate = ageCutoffDate;
            profile.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return profile;
        }
    }
}
