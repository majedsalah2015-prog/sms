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
        /// BR-INS-002/004: generates the schedule against the student's posted charges (net of credit notes) in the working
        /// year, rounding into the last installment, shifting due dates off non-working days. Throws
        /// <see cref="Common.Exceptions.PlanTemplateNotApprovedException"/>, <see cref="Common.Exceptions.NoChargesToScheduleException"/>,
        /// <see cref="Common.Exceptions.PlanAssignmentExistsException"/>; an exception assignment demands a reason.
        /// </summary>
        Task<PlanAssignment> AssignPlanAsync(
            int studentId, int payerId, int planTemplateId, ISet<DayOfWeek> weekendDays,
            bool isException = false, string? exceptionReason = null, CancellationToken cancellationToken = default);

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
