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

        /// <summary>Ladder builder: sets/clears a grade's promotion target and graduating flag; throws <see cref="Common.Exceptions.PromotionPathCycleException"/> if the path would loop (doc/Modules/05 §9 acyclic).</summary>
        Task SetPromotionPathAsync(int gradeLevelId, int? promotionTargetGradeLevelId, bool isGraduating, CancellationToken cancellationToken = default);

        /// <summary>Edits a stage's names/order/default gender. Widening the gender policy is always allowed; narrowing is refused while any grade-year profile of the stage would stop being a valid narrowing (BR-GRD-004).</summary>
        Task<Stage> UpdateStageAsync(int stageId, string nameAr, string nameEn, int sequenceOrder, GenderPolicy defaultGenderPolicy, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes (deactivates) a stage; throws <see cref="Common.Exceptions.GradeStructureInUseException"/> while it still has active grades.</summary>
        Task DeactivateStageAsync(int stageId, CancellationToken cancellationToken = default);

        /// <summary>Edits a grade level's stage/code/names/order (promotion path is edited via <see cref="SetPromotionPathAsync"/>). Throws <see cref="Common.Exceptions.DuplicateGradeCodeException"/> on a repeated code.</summary>
        Task<GradeLevel> UpdateGradeLevelAsync(int gradeLevelId, int stageId, string code, string nameAr, string nameEn, int sequenceOrder, CancellationToken cancellationToken = default);

        /// <summary>BR-GRD-007: soft-deletes (deactivates) a grade level; throws <see cref="Common.Exceptions.GradeStructureInUseException"/> when any of its year profiles has enrollments/sections, or another grade promotes into it.</summary>
        Task DeactivateGradeLevelAsync(int gradeLevelId, CancellationToken cancellationToken = default);

        /// <summary>Removes (deactivates) a grade-year profile; throws <see cref="Common.Exceptions.GradeStructureInUseException"/> when it has enrollments or sections.</summary>
        Task RemoveGradeYearProfileAsync(int gradeYearProfileId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidGenderPolicyNarrowingException"/> when the requested policy widens the grade's stage default.</summary>
        Task<GradeYearProfile> DefineGradeYearProfileAsync(
            int gradeLevelId, int academicYearId, GenderPolicy genderPolicy, int targetSections, int targetSectionSize,
            int? curriculumLookupValueId = null, decimal? minAgeAtCutoff = null, decimal? maxAgeAtCutoff = null,
            DateTime? ageCutoffDate = null, CancellationToken cancellationToken = default);
    }
}
