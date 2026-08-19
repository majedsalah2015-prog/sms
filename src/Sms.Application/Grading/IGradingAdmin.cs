using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Grading;

namespace Sms.Application.Grading
{
    /// <summary>
    /// doc/Modules/17 §8 Scale designer / Blueprint editor / Marksheet
    /// workspace screens backing (screens deferred, the operations are
    /// core). Report card PDF generation (BR-GRA-008) needs the O6
    /// PDF-engine decision (QuestPDF vs Syncfusion vs DevExpress — still
    /// open, never spiked) and is entirely out of this slice; publishing a
    /// Marksheet produces computed TermResult rows (the data a report
    /// card would render from), not a document.
    /// </summary>
    public interface IGradingAdmin
    {
        /// <summary>BR-GRA-001: scales are year-versioned — <paramref name="academicYearId"/> defaults to the school's Active year when omitted.</summary>
        Task<GradingScale> DefineScaleAsync(
            int stageId, string nameAr, string nameEn, int? curriculumLookupValueId = null, int? academicYearId = null, CancellationToken cancellationToken = default);


        /// <summary>Throws <see cref="Common.Exceptions.GradingScaleLockedException"/>.</summary>
        Task<ScaleBand> AddScaleBandAsync(
            int gradingScaleId, decimal minPercent, decimal maxPercent, string bandCode, string labelAr, string labelEn,
            bool isPassing, int sortOrder, decimal? gpaPoints = null, CancellationToken cancellationToken = default);

        Task LockScaleAsync(int gradingScaleId, CancellationToken cancellationToken = default);

        Task<Blueprint> DefineBlueprintAsync(
            int curriculumOfferingId, int termId, int gradingScaleId, bool redistributeWeightOnExemption = false,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.BlueprintLockedException"/> if the blueprint is already finalized.</summary>
        Task<BlueprintComponent> AddBlueprintComponentAsync(
            int blueprintId, string nameAr, string nameEn, decimal weight, decimal maxScore, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.BlueprintWeightMismatchException"/> unless component weights sum to exactly 100.</summary>
        Task LockBlueprintAsync(int blueprintId, CancellationToken cancellationToken = default);

        /// <summary>Seeds one MarkEntry stub per current section member x blueprint component. Throws <see cref="Common.Exceptions.BlueprintNotFinalizedException"/>.</summary>
        Task<Marksheet> CreateMarksheetAsync(int blueprintId, int sectionId, CancellationToken cancellationToken = default);

        Task EnterMarkAsync(
            int marksheetId, int blueprintComponentId, int enrollmentId, decimal? score, bool isAbsent, bool isExempt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Throws <see cref="Common.Exceptions.InvalidMarksheetStatusTransitionException"/>, or
        /// <see cref="Common.Exceptions.UnresolvedMarkEntriesException"/> when moving to Published with
        /// unresolved entries. Publishing computes and persists a TermResult per enrollment (BR-GRA-003).
        /// </summary>
        Task ChangeMarksheetStatusAsync(int marksheetId, MarksheetStatus newStatus, CancellationToken cancellationToken = default);

        /// <summary>BR-GRA-005 WF-08: Published -> Draft, reason mandatory (P4 Principal chain not enforced here). Re-entry + re-publish reuse EnterMarkAsync/ChangeMarksheetStatusAsync as normal.</summary>
        Task CorrectPublishedMarksheetAsync(int marksheetId, string reason, CancellationToken cancellationToken = default);

        Task<PromotionCriteria> DefinePromotionCriteriaAsync(
            int gradeYearProfileId, decimal overallPassMark, int maxFailedSubjectsForPromotion, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-GRA-006/007: aggregates the enrollment's TermResults for the
        /// year into a GPA + promotion outcome (latest TermResult per
        /// offering stands in for full term-weighted year aggregation —
        /// BR-GRA-003's configurable term-weight scheme isn't implemented
        /// in this slice). Requires a PromotionCriteria row for the
        /// enrollment's grade-year profile.
        /// </summary>
        Task<YearResult> ComputeYearResultAsync(int enrollmentId, int academicYearId, int gradeYearProfileId, CancellationToken cancellationToken = default);

        // ---- E-302 screen support (edit / delete per the module's own §8 designer screens) ----

        /// <summary>Renames a scale (T1: names are reason-required once the row exists — set the ambient audit reason).</summary>
        Task<GradingScale> UpdateScaleAsync(int gradingScaleId, string nameAr, string nameEn, CancellationToken cancellationToken = default);

        /// <summary>Hard-deletes an unlocked scale and its bands. Throws <see cref="Common.Exceptions.GradingScaleLockedException"/> or <see cref="Common.Exceptions.GradingScaleInUseException"/> when a blueprint references it.</summary>
        Task DeleteScaleAsync(int gradingScaleId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.GradingScaleLockedException"/>.</summary>
        Task<ScaleBand> UpdateScaleBandAsync(
            int scaleBandId, decimal minPercent, decimal maxPercent, string bandCode, string labelAr, string labelEn,
            bool isPassing, int sortOrder, decimal? gpaPoints = null, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.GradingScaleLockedException"/>.</summary>
        Task RemoveScaleBandAsync(int scaleBandId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.BlueprintLockedException"/>.</summary>
        Task<BlueprintComponent> UpdateBlueprintComponentAsync(
            int blueprintComponentId, string nameAr, string nameEn, decimal weight, decimal maxScore, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.BlueprintLockedException"/>.</summary>
        Task RemoveBlueprintComponentAsync(int blueprintComponentId, CancellationToken cancellationToken = default);

        /// <summary>Hard-deletes an unlocked blueprint and its components. Throws <see cref="Common.Exceptions.BlueprintLockedException"/> or <see cref="Common.Exceptions.BlueprintInUseException"/> when a marksheet exists for it.</summary>
        Task DeleteBlueprintAsync(int blueprintId, CancellationToken cancellationToken = default);

        /// <summary>Saves a whole grid in one unit of work (the marksheet workspace's "save progress"). Entries not in the batch are untouched.</summary>
        Task EnterMarksAsync(int marksheetId, System.Collections.Generic.IReadOnlyList<MarkInput> marks, CancellationToken cancellationToken = default);

        /// <summary>Hard-deletes a Draft marksheet with no marks entered yet (BR-GRA-011: marks are T1 from first entry — once any score exists the sheet stays). Throws <see cref="Common.Exceptions.MarksheetInUseException"/>.</summary>
        Task DeleteMarksheetAsync(int marksheetId, CancellationToken cancellationToken = default);
    }

    /// <summary>One cell of the marksheet grid for <see cref="IGradingAdmin.EnterMarksAsync"/>.</summary>
    public sealed record MarkInput(int BlueprintComponentId, int EnrollmentId, decimal? Score, bool IsAbsent, bool IsExempt);
}
