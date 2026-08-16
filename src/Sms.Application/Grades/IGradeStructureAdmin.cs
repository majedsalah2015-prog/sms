using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Grades;

namespace Sms.Application.Grades
{
    /// <summary>doc/Modules/05 §8 "Ladder builder"/"Grade-year profile editor" screens backing (screens deferred, the operations are core).</summary>
    public interface IGradeStructureAdmin
    {
        Task<Stage> DefineStageAsync(
            string nameAr, string nameEn, int sequenceOrder, GenderPolicy defaultGenderPolicy, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.DuplicateGradeCodeException"/> on a repeated code.</summary>
        Task<GradeLevel> DefineGradeLevelAsync(
            int stageId, string code, string nameAr, string nameEn, int sequenceOrder,
            int? promotionTargetGradeLevelId, bool isGraduating, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidGenderPolicyNarrowingException"/> when the requested policy widens the grade's stage default.</summary>
        Task<GradeYearProfile> DefineGradeYearProfileAsync(
            int gradeLevelId, int academicYearId, GenderPolicy genderPolicy, int targetSections, int targetSectionSize,
            int? curriculumLookupValueId = null, decimal? minAgeAtCutoff = null, decimal? maxAgeAtCutoff = null,
            DateTime? ageCutoffDate = null, CancellationToken cancellationToken = default);
    }
}
