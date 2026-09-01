using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Fees;

namespace Sms.Application.Fees
{
    /// <summary>
    /// One off-price-list item added to the basket: a service the grade's structure does not
    /// carry. BR-FEE-003 makes <paramref name="Reason"/> mandatory for exactly this case — a
    /// manual charge is the one figure in the system nobody can re-derive from a price list.
    /// </summary>
    public sealed record ManualFeeItem(int FeeCategoryId, decimal Amount, string Reason);

    /// <summary>
    /// The discount half of the basket. Proposed and approved in the same commit, which is
    /// what makes it visible on the net figure the clerk was shown — a grant left Proposed
    /// reduces nothing (BR-DIS-005).
    /// </summary>
    public sealed record DiscountRequest(int DiscountTypeId, decimal BasisValue, string Reason, bool HasHardshipDocumentation = false);

    /// <summary>
    /// Everything the clerk ticked on the student's financial file before pressing
    /// "approve". <see cref="FeeCategoryIds"/> are billed at their approved structure price
    /// (BR-FEE-002 — an approved amount is immutable, so the basket never carries an
    /// override); anything at another figure is an <see cref="ExtraItem"/> and is a manual
    /// charge with its own reason.
    /// </summary>
    public sealed record StudentFeeFileCommit(
        int StudentId,
        int ParentId,
        IReadOnlyList<int> FeeCategoryIds,
        ManualFeeItem? ExtraItem,
        int? PlanTemplateId,
        DiscountRequest? Discount,
        ISet<DayOfWeek> WeekendDays);

    /// <summary>What the commit actually did, so the screen reports figures rather than "saved".</summary>
    public sealed record StudentFeeFileCommitResult(
        IReadOnlyList<int> PostedChargeIds,
        decimal PostedGross,
        int? PlanAssignmentId,
        int InstallmentCount,
        int? DiscountGrantId,
        decimal DiscountApplied)
    {
        public int ItemCount => PostedChargeIds.Count;
    }

    /// <summary>
    /// doc/Modules/19 §8.7 written from the counter's side: the fee items, the installment
    /// template and the discount chosen together on the student's own file and committed as
    /// one act (owner request, 2026-08-31).
    /// <para>
    /// It owns no rules of its own — it composes <see cref="IFeeAdmin"/>,
    /// <see cref="Installments.IInstallmentAdmin"/> and <see cref="Discounts.IDiscountAdmin"/>
    /// in the one order their own rules permit: charges first, because a schedule is generated
    /// against posted charges (BR-INS-002) and a discount document is issued against them
    /// (BR-DIS-005); the plan next; the discount last, because approving it reduces the
    /// forward installments the plan just created (BR-INS-003). Each of those saves itself, so
    /// the whole sequence runs inside one explicit transaction — a basket that posted the fees
    /// and then failed to schedule them would leave a family billed for a plan they were never
    /// given.
    /// </para>
    /// <para>
    /// There is no draft state behind this. The basket lives in the form until it is approved,
    /// and after that every line is a posted invoice: BR-GLB-005 leaves no delete verb, so
    /// <see cref="RemoveItemAsync"/> and <see cref="AdjustItemAsync"/> are credit notes.
    /// </para>
    /// </summary>
    public interface IStudentFeeFileService
    {
        /// <summary>
        /// Bills the ticked items, generates the schedule and applies the discount, in one
        /// transaction, and refuses the lot if any part refuses. Throws the module's own
        /// refusals unchanged — <see cref="Common.Exceptions.FeeStructureLineNotApprovedException"/>,
        /// <see cref="Common.Exceptions.NoChargesToScheduleException"/>,
        /// <see cref="Common.Exceptions.PlanAssignmentExistsException"/>,
        /// <see cref="Common.Exceptions.DiscountStackingViolationException"/> and the rest — so
        /// the Web boundary translates one vocabulary rather than a wrapper's.
        /// <para>
        /// Throws <see cref="Common.Exceptions.EmptyFeeFileCommitException"/> when nothing was
        /// ticked, <see cref="Common.Exceptions.StudentNotEnrolledForFeeFileException"/> when
        /// the student has no live enrollment in the working year to price against, and
        /// <see cref="Common.Exceptions.FeeItemAlreadyBilledException"/> when a ticked category
        /// was billed between the screen being drawn and the button being pressed.
        /// </para>
        /// </summary>
        Task<StudentFeeFileCommitResult> CommitAsync(StudentFeeFileCommit request, CancellationToken cancellationToken = default);

        /// <summary>
        /// "Edit this item": brings a posted item down to <paramref name="newGrossAmount"/> by
        /// crediting the difference. A posted invoice is immutable (BR-GLB-062), so the figure
        /// on screen changes by a document that says why, not by an UPDATE.
        /// <para>
        /// Refuses to raise an item — that is a new charge, not a correction — with
        /// <see cref="Common.Exceptions.FeeItemAdjustmentNotLowerException"/>.
        /// </para>
        /// </summary>
        Task<CreditNote> AdjustItemAsync(int chargeId, decimal newGrossAmount, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// "Remove this item": credits its whole remaining value, so the item stops being owed
        /// while the invoice and the reason stay readable (BR-GLB-005).
        /// <para>
        /// The credit is net of what discount documents already took off, so removing a
        /// discounted item relieves it exactly once. Money already received against it is not
        /// reversed here — that leaves the payer in credit, which is Module 21's refund or
        /// re-allocation to settle, and is the honest outcome rather than a silent write-back.
        /// </para>
        /// <para>Throws <see cref="Common.Exceptions.ChargeAlreadyFullyRelievedException"/> when nothing is left to credit.</para>
        /// </summary>
        Task<CreditNote> RemoveItemAsync(int chargeId, string reason, CancellationToken cancellationToken = default);
    }
}
