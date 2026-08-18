using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Rollover
{
    /// <summary>
    /// core.RolloverBatch (doc/Modules/03 §7): one year-end rollover run
    /// from a source (Active) year into a target (Preparation) year. Owns
    /// the step-level state; per-student progress lives in
    /// <see cref="RolloverStudentState"/> (BR-AYR-008: resumable, idempotent
    /// per student, fully progress-tracked).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class RolloverBatch : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int SourceAcademicYearId { get; set; }

        public int TargetAcademicYearId { get; set; }

        public RolloverBatchStatus Status { get; set; } = RolloverBatchStatus.Open;

        /// <summary>Step 3 P3 approval of the promotion batch.</summary>
        public DateTime? PromotionsApprovedAtUtc { get; set; }

        public int? PromotionsApprovedByUserId { get; set; }

        /// <summary>
        /// BR-AYR-004: "timetable published or explicitly deferred (permission)" —
        /// a non-null reason records the explicit deferral that satisfies the
        /// opening-checklist item without a published timetable.
        /// </summary>
        public string? TimetableDeferredReason { get; set; }

        public DateTime? ActivatedAtUtc { get; set; }

        /// <summary>BR-AYR-009 carry-forward posting run (step 7 prerequisite).</summary>
        public DateTime? CarryForwardPostedAtUtc { get; set; }

        /// <summary>Σ opening-balance charges posted by this batch — must equal Σ closing receivables (doc §9 hard check).</summary>
        public decimal? CarryForwardTotal { get; set; }

        public DateTime? ClosedAtUtc { get; set; }
    }
}
