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
