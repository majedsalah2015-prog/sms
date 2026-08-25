using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Installments;

namespace Sms.Application.Installments
{
    /// <summary>BR-INS-001 split-rule input: percentage + an absolute due date or an offset from year start.</summary>
    public sealed record TemplateSplit(decimal Percent, DateTime? DueDate = null, int? OffsetDaysFromYearStart = null);

    /// <summary>BR-INS-005 proposed installment for the unpaid remainder.</summary>
    public sealed record ProposedInstallment(DateTime DueDate, decimal Amount);

    /// <summary>BR-INS-007 derived view — the only place a status ever exists.</summary>
    public sealed record InstallmentView(
        int InstallmentId, int SequenceNumber, DateTime DueDate, decimal Amount, decimal Paid, InstallmentStatus Status, bool IsPdcCovered);

    /// <summary>
    /// BR-INS-002 grade-wide run: what happened, or would happen, to one student.
    /// Every value is reachable — a grade is never uniform, and the officer needs
    /// to see who was left behind and why rather than a bare success count.
    /// </summary>
    public enum GradeAssignmentOutcome
    {
        /// <summary>Preview only: this student would get a schedule.</summary>
        Ready = 1,

        /// <summary>A schedule was generated.</summary>
        Assigned = 2,

        /// <summary>Already carries a plan for this year and category group — left alone, never rewritten.</summary>
        AlreadyPlanned = 3,

        /// <summary>No posted mandatory charges, or none left once credit notes and discounts are taken off.</summary>
        NoMandatoryCharges = 4,

        /// <summary>
        /// The student's mandatory charges are billed to more than one payer (BR-FEE-004
        /// sponsor billing does exactly this). A schedule is addressed to one payer, so
        /// picking one here would silently leave the other's charges unscheduled — this
        /// student is left for the single-student console.
        /// </summary>
        PayerSplit = 5,
    }

    /// <summary>One student's line in a grade-wide run. Ids only: the caller already knows how to name a student.</summary>
    public sealed record GradeAssignmentLine(
        int StudentId, int? PayerId, GradeAssignmentOutcome Outcome, decimal MandatoryTotal, int? PlanAssignmentId);

    /// <summary>BR-INS-002 grade-wide run, previewed or committed.</summary>
    public sealed record GradeAssignmentRun(int GradeLevelId, int PlanTemplateId, IReadOnlyList<GradeAssignmentLine> Lines)
    {
        public int Count(GradeAssignmentOutcome outcome)
        {
            var n = 0;
            foreach (var line in Lines)
            {
                if (line.Outcome == outcome)
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>
    /// doc/Modules/20 §8 Template designer / Assignment console / Family
    /// schedule view / Reschedule wizard / Dunning console screens backing
    /// (screens deferred, the operations are core). Late-fee computation
    /// (Module 19 consumes overdue facts from here) and discounts (Module
    /// 22, E-502) are consumers of this module, not part of it.
    /// </summary>
    public interface IInstallmentAdmin
    {
        /// <summary>Throws <see cref="Common.Exceptions.InvalidTemplateSplitException"/> unless the splits sum to 100 and every split has a due-date rule (BR-INS-001, doc §9).</summary>
        Task<PlanTemplate> DefineTemplateAsync(
            int academicYearId, string nameAr, string nameEn, IReadOnlyList<TemplateSplit> splits,
            int? feeCategoryId = null, decimal downPaymentPercent = 0m, int graceDays = 0, CancellationToken cancellationToken = default);

        Task ApproveTemplateAsync(int planTemplateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rewrites a <b>draft</b> template's definition and splits, under the same
        /// BR-INS-001 rules <see cref="DefineTemplateAsync"/> applies.
        /// <para>
        /// Draft only, and that is the whole guarantee: an approved template may
        /// already have generated schedules, and a schedule is materialised once at
        /// assignment and never re-derived. Editing an approved template would
        /// therefore change what new families get while leaving existing ones on the
        /// old shape — two meanings for one name. Approve is the point of no return;
        /// after it, a different plan is a different template.
        /// </para>
        /// </summary>
        Task<PlanTemplate> UpdateTemplateAsync(
            int planTemplateId, string nameAr, string nameEn, IReadOnlyList<TemplateSplit> splits,
            int? feeCategoryId = null, decimal downPaymentPercent = 0m, int graceDays = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a template nothing has been assigned from. Throws
        /// <see cref="Common.Guards.RecordInUseException"/> otherwise, carrying what
        /// is in the way.
        /// <para>
        /// A hard delete, deliberately, against this system's usual soft-delete
        /// stance (ADR-7): that rule protects <i>transacted</i> data, and a template
        /// nothing was ever assigned from has transacted nothing. Keeping such rows
        /// forever would leave the designer full of abandoned drafts with no way to
        /// clear them.
        /// </para>
        /// </summary>
        Task DeleteTemplateAsync(int planTemplateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-INS-002/004: generates the schedule against the student's posted charges (net of credit notes) in the working
        /// year, rounding into the last installment, shifting due dates off non-working days. Throws
        /// <see cref="Common.Exceptions.PlanTemplateNotApprovedException"/>, <see cref="Common.Exceptions.NoChargesToScheduleException"/>,
        /// <see cref="Common.Exceptions.PlanAssignmentExistsException"/>; an exception assignment demands a reason.
        /// </summary>
        Task<PlanAssignment> AssignPlanAsync(
            int studentId, int payerId, int planTemplateId, ISet<DayOfWeek> weekendDays,
            bool isException = false, string? exceptionReason = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// doc §8.2 "defaults per grade" / BR-INS-002: what a grade-wide run would do,
        /// student by student, without writing anything. Same evaluation
        /// <see cref="AssignPlanToGradeAsync"/> performs, so the preview and the run cannot
        /// disagree.
        /// <para>
        /// Throws <see cref="Common.Exceptions.PlanTemplateNotApprovedException"/>, and
        /// <see cref="Common.Exceptions.TemplateCategoryNotMandatoryException"/> when the
        /// template is scoped to a fee category that is not mandatory.
        /// </para>
        /// </summary>
        Task<GradeAssignmentRun> PreviewGradeAssignmentAsync(
            int gradeLevelId, int planTemplateId, CancellationToken cancellationToken = default);

        /// <summary>
        /// doc §8.2 / BR-INS-002: generates a schedule for every actively-enrolled student of
        /// one grade level in the working year, over that student's posted <b>mandatory</b>
        /// charges only (net of credit notes and discounts) — the fees the school bills every
        /// child of the grade, as opposed to the optional services a family adds one at a time.
        /// <para>
        /// Each student is one committed unit: a student the run cannot schedule is reported on
        /// their own line and never stops the rest of the grade. Assignments are ordinary
        /// defaults, not exceptions (BR-INS-002 makes the per-family exception the deliberate
        /// gesture, and a grade-wide default is the opposite of one). Students who already carry
        /// a plan for this year and category group are left untouched.
        /// </para>
        /// <para>Throws the same two refusals as <see cref="PreviewGradeAssignmentAsync"/>.</para>
        /// </summary>
        Task<GradeAssignmentRun> AssignPlanToGradeAsync(
            int gradeLevelId, int planTemplateId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-007: statuses derived as of now from Module 21 allocations.</summary>
        Task<IReadOnlyList<InstallmentView>> GetScheduleAsync(int planAssignmentId, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-003: a service added mid-year — spread evenly over the open (unpaid, non-superseded) installments, remainder in the last; logged with before/after snapshot.</summary>
        Task AppendChargeAsync(int planAssignmentId, int chargeId, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-003: credit note / discount — reduces future installments first, then last-to-first; paid portions never mutate; logged with before/after snapshot.</summary>
        Task ReduceScheduleAsync(int planAssignmentId, decimal reduction, string reason, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-005: proposal must cover exactly the unpaid remainder (<see cref="Common.Exceptions.RescheduleRemainderMismatchException"/>); RequiresPrincipal set per <see cref="RescheduleApprovalRouter"/>.</summary>
        Task<RescheduleCase> ProposeRescheduleAsync(
            int planAssignmentId, int proposedByUserId, string reason, IReadOnlyList<ProposedInstallment> proposal,
            ISet<DayOfWeek> weekendDays, int maxExtensionMonths = 3, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-005: approval supersedes the unpaid installments (kept in history) and materializes the proposal; increments the family's reschedule count.</summary>
        Task DecideRescheduleAsync(int rescheduleCaseId, bool approve, string? decisionReason = null, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-006: promised date must be today..today+horizon (<see cref="Common.Exceptions.PromiseDateOutOfRangeException"/>) and the installment truly overdue.</summary>
        Task<PromiseToPay> RecordPromiseAsync(int installmentId, int recordedByUserId, DateTime promisedDate, decimal amount, int horizonDays = 30, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-006: resolves open promises whose date has passed — Kept if the installment is paid, Broken otherwise. Returns the number broken.</summary>
        Task<int> EvaluatePromisesAsync(CancellationToken cancellationToken = default);

        /// <summary>BR-INS-009: the PDC must belong to the assignment's payer and be live (Lodged/Due/Deposited) — <see cref="Common.Exceptions.PdcNotCoverableException"/>.</summary>
        Task MarkPdcCoveredAsync(int installmentId, int pdcId, CancellationToken cancellationToken = default);

        /// <summary>WF-06 write-off (P4 chain not enforced here; reason mandatory, T1).</summary>
        Task WriteOffAsync(int installmentId, string reason, CancellationToken cancellationToken = default);

        /// <summary>BR-INS-008: one ladder pass over every open installment as of now — evaluates promises first, fires at most one step per installment, logs a DunningEvent, publishes InstallmentDueSoon/InstallmentOverdue to the payer.</summary>
        Task<IReadOnlyList<DunningEvent>> RunDunningAsync(CancellationToken cancellationToken = default);
    }
}
