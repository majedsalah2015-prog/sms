using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Rollover
{
    /// <summary>
    /// core.RolloverStudentState (doc/Modules/03 §7): per-student step
    /// status inside one <see cref="RolloverBatch"/> — "makes BR-AYR-008
    /// idempotency concrete". One row per (batch, student), unique.
    /// Every step is re-runnable: a step only touches rows that haven't
    /// completed that step yet (the nullable "…At/…Id" columns below are
    /// the idempotency markers).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class RolloverStudentState : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RolloverBatchId { get; set; }

        public int StudentId { get; set; }

        /// <summary>The student's Active enrollment in the source year at batch-open time.</summary>
        public int SourceEnrollmentId { get; set; }

        public int SourceGradeYearProfileId { get; set; }

        // ---- Step 3: promotion decision ----

        /// <summary>Auto proposal from Module 17 (kept even after a manual override, for the exceptions view).</summary>
        public PromotionDecision ProposedDecision { get; set; } = PromotionDecision.Undecided;

        public PromotionDecision Decision { get; set; } = PromotionDecision.Undecided;

        public PromotionDecisionSource DecisionSource { get; set; } = PromotionDecisionSource.None;

        /// <summary>Mandatory on a manual override.</summary>
        public string? DecisionReason { get; set; }

        /// <summary>Target-year grade profile implied by the decision (null while Undecided/Graduate).</summary>
        public int? TargetGradeYearProfileId { get; set; }

        // ---- Step 4: re-registration ----

        public ReRegistrationStatus ReRegistration { get; set; } = ReRegistrationStatus.Pending;

        public DateTime? ReRegistrationDecidedAtUtc { get; set; }

        /// <summary>BR-FEE-003 re-registration charge posted into the Preparation year (BR-AYR-003 explicitly allows it).</summary>
        public int? ReRegistrationChargeId { get; set; }

        // ---- Step 5: section assignment ----

        /// <summary>Planned target-year section; the SectionMembership itself materializes at activation.</summary>
        public int? AssignedSectionId { get; set; }

        // ---- Step 6: activation ----

        /// <summary>The new-year Enrollment (Promote/Conditional/Retain) — set means "this student is done".</summary>
        public int? TargetEnrollmentId { get; set; }

        /// <summary>Set for every student the activation pass has processed (incl. graduates/declined, which get no enrollment).</summary>
        public DateTime? ActivatedAtUtc { get; set; }

        // ---- Step 7: carry-forward ----

        /// <summary>Σ opening-balance charges posted for this student by the batch (BR-AYR-009).</summary>
        public decimal? CarryForwardAmount { get; set; }
    }
}
