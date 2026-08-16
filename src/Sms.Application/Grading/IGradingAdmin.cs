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
        Task<GradingScale> DefineScaleAsync(
            int stageId, string nameAr, string nameEn, int? curriculumLookupValueId = null, CancellationToken cancellationToken = default);

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
    }
}
