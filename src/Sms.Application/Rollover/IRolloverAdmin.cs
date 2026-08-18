using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Rollover;

namespace Sms.Application.Rollover
{
    /// <summary>
    /// doc/Modules/03 §4 — the year-end rollover workflow (WF-02 family),
    /// BR-AYR-008: "the only path by which existing students enter the next
    /// year; resumable, idempotent per student, fully progress-tracked."
    /// Standalone admin: every method saves itself. Every step is re-runnable
    /// for stragglers without touching students who already completed it.
    /// </summary>
    public interface IRolloverAdmin
    {
        /// <summary>
        /// Steps 1–2 glue: opens a batch from the school's Active year into the given Preparation year, copies the
        /// source year's grade-year profiles into the target (upsert), validates the grade promotion path for every
        /// grade that has enrolled students, and seeds one <see cref="RolloverStudentState"/> per Active source
        /// enrollment. Re-calling for the same pair returns the existing batch and only adds missing students.
        /// </summary>
        Task<RolloverBatch> OpenBatchAsync(int sourceAcademicYearId, int targetAcademicYearId, CancellationToken cancellationToken = default);

        /// <summary>Step 3: auto-propose from Module 17's YearResult for every student not manually decided. Returns the number proposed.</summary>
        Task<int> ProposePromotionsAsync(int batchId, CancellationToken cancellationToken = default);

        /// <summary>Step 3: Registrar/Principal override for one student (reason mandatory). Allowed until activation.</summary>
        Task DecideAsync(int batchId, int studentId, PromotionDecision decision, string reason, CancellationToken cancellationToken = default);

        /// <summary>Step 3: Principal approves the batch (P3) — refused while any student is Undecided.</summary>
        Task ApprovePromotionsAsync(int batchId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Step 4: parent confirms; seat reserved against the target grade's planned seats when the target is known
        /// (doc Q4: confirming before the decision is allowed); re-registration fee posted into the Preparation year
        /// (BR-AYR-003) when <paramref name="reRegistrationFeeCategoryId"/> is given and an approved structure line
        /// exists. Idempotent — a second call for a Confirmed student is a no-op.
        /// </summary>
        Task ConfirmReRegistrationAsync(int batchId, int studentId, int? reRegistrationFeeCategoryId = null, CancellationToken cancellationToken = default);

        /// <summary>Step 4: "Not Re-registering" — releases any reserved seat / planned section; feeds WF-03.</summary>
        Task DeclineReRegistrationAsync(int batchId, int studentId, CancellationToken cancellationToken = default);

        /// <summary>BR-AYR-004: record the explicit timetable deferral that satisfies the opening checklist without a published timetable.</summary>
        Task DeferTimetableAsync(int batchId, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Step 5: rule-based auto-distribution (BR-SCN-008 size/gender subset) of confirmed, decided, unassigned
        /// students of one target grade-year profile across its sections. Returns the students it could not place.
        /// </summary>
        Task<IReadOnlyList<int>> AutoAssignSectionsAsync(int batchId, int targetGradeYearProfileId, CancellationToken cancellationToken = default);

        /// <summary>Step 5: manual placement (drag adjustment); capacity enforced against memberships + planned assignments.</summary>
        Task AssignSectionAsync(int batchId, int studentId, int sectionId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChecklistItem>> GetOpeningChecklistAsync(int batchId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Step 6: opening checklist must be green (BR-AYR-004). Materializes one Rollover enrollment + section membership
        /// per confirmed student, closes the source enrollment, graduates graduating students, then activates the target
        /// year (source → Closing with a <paramref name="closingWindowDays"/> window, BR-AYR-005). Processes students one
        /// committed unit at a time and honours cancellation between students — a killed run resumes where it stopped
        /// and never double-enrolls (BR-AYR-008). <paramref name="progress"/> receives the running processed count.
        /// </summary>
        Task ActivateAsync(int batchId, int closingWindowDays = 60, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Step 7a (BR-AYR-009 / BR-FEE-009): for every student with a positive source-year receivable, post one
        /// OpeningBalance charge per payer into the target year (referencing the source year) and a carry-forward credit
        /// note per source charge remainder; hard-checks that both totals reconcile. Idempotent per (student, payer).
        /// Returns the total carried forward.
        /// </summary>
        Task<decimal> PostCarryForwardAsync(int batchId, int openingBalanceFeeCategoryId, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChecklistItem>> GetClosingChecklistAsync(int batchId, CancellationToken cancellationToken = default);

        /// <summary>Step 7b: closing checklist must be green (BR-AYR-005); moves the source year Closing → Closed.</summary>
        Task CloseSourceYearAsync(int batchId, CancellationToken cancellationToken = default);

        Task<RolloverProgress> GetProgressAsync(int batchId, CancellationToken cancellationToken = default);
    }
}
