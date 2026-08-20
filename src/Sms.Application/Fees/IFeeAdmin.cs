using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Fees;

namespace Sms.Application.Fees
{
    /// <summary>
    /// doc/Modules/19 §8 Category catalog / Fee structure workbench /
    /// Charge explorer / Credit note flow screens backing (screens
    /// deferred, the operations are core). PostChargeAsync populates
    /// BR-FEE-005's e-invoicing-readiness fields for real (UUID + SHA-256
    /// hash chain via InvoiceHashChainBuilder) — live ZATCA submission is
    /// out of scope.
    /// </summary>
    public interface IFeeAdmin
    {
        Task<FeeCategory> DefineCategoryAsync(
            string nameAr, string nameEn, decimal? vatRate, bool isMandatory, bool isRefundable, bool isServiceLinked,
            string? glExportCode = null, CancellationToken cancellationToken = default);

        /// <summary>E-303 screens (8.1 Category catalog): edit a category in place. FeeCategory is T2 — no reason required.</summary>
        Task UpdateCategoryAsync(
            int feeCategoryId, string nameAr, string nameEn, decimal? vatRate, bool isMandatory, bool isRefundable, bool isServiceLinked,
            string? glExportCode = null, CancellationToken cancellationToken = default);

        /// <summary>Soft-deactivates a category. Throws <see cref="Common.Exceptions.FeeCategoryInUseException"/> when structure lines or charges reference it.</summary>
        Task DeactivateCategoryAsync(int feeCategoryId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.FeeStructureLineAlreadyExistsException"/> when the grade-year × category pair already has a line.</summary>
        Task<FeeStructureLine> DefineStructureLineAsync(
            int gradeYearProfileId, int feeCategoryId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>Draft lines only (BR-FEE-002 — an approved amount is immutable). Amount is T1 reason-required. Throws <see cref="Common.Exceptions.FeeStructureLineNotDraftException"/>.</summary>
        Task UpdateStructureLineAsync(int feeStructureLineId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>Draft lines only. Throws <see cref="Common.Exceptions.FeeStructureLineNotDraftException"/>.</summary>
        Task DeleteStructureLineAsync(int feeStructureLineId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 8.2 "copy-from-last-year with % uplift": for every approved source-year line whose grade level has a
        /// profile in the target year and no line yet for that category, creates a Draft line at amount × (1 + uplift).
        /// Returns the number of lines created.
        /// </summary>
        Task<int> CopyStructureAsync(int sourceAcademicYearId, int targetAcademicYearId, decimal upliftPercent, CancellationToken cancellationToken = default);

        /// <summary>Returns the parent's Payer row, creating it on first use (registration never materializes payers — the first charge does).</summary>
        Task<Payer> EnsurePayerForParentAsync(int parentId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidFeeStructureLineStatusTransitionException"/>.</summary>
        Task ApproveStructureLineAsync(int feeStructureLineId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.FeeStructureLineNotApprovedException"/> when no approved line covers the student's grade-year x category.</summary>
        Task<Charge> PostChargeAsync(
            int studentId, int payerId, int gradeYearProfileId, int feeCategoryId, ChargeSourceType sourceType,
            CancellationToken cancellationToken = default);

        /// <summary>BR-FEE-003: manual/misc charges post an explicit amount rather than reading a structure line.</summary>
        Task<Charge> PostManualChargeAsync(
            int studentId, int payerId, int feeCategoryId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-FEE-009 / BR-AYR-009 (S8/E-801): posts an OpeningBalance charge into <paramref name="targetAcademicYearId"/>
        /// referencing <paramref name="sourceAcademicYearId"/>. No VAT — a receivable transfer is not a supply.
        /// Ambient (does NOT save): the rollover commits it together with the carry-forward credit notes it mirrors,
        /// so the pair is atomic (BR-AYR §9 reconciliation hard check).
        /// </summary>
        Task<Charge> PostOpeningBalanceAsync(
            int studentId, int payerId, int targetAcademicYearId, int sourceAcademicYearId, int feeCategoryId, decimal amount,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.ChargeNotPostedException"/> or <see cref="Common.Exceptions.CreditNoteExceedsChargeException"/>.</summary>
        Task<CreditNote> IssueCreditNoteAsync(int chargeId, decimal amount, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Voids an untouched posted charge (screens' "delete"). Allowed only while nothing references the document —
        /// no allocated payments, credit notes or discounts (throws <see cref="Common.Exceptions.ChargeHasActivityException"/>);
        /// otherwise the credit-note path is the only correction (BR-GLB-062). The row itself is kept (numbered tax
        /// document, hash chain stays intact) with Status=Void, which removes it from every position/receivables reader.
        /// Caller supplies the mandatory reason via the ambient <c>IAuditContext</c> (Charge is T1), same as amount edits.
        /// Throws <see cref="Common.Exceptions.ChargeNotPostedException"/> when not Posted.
        /// </summary>
        Task VoidChargeAsync(int chargeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-AYR-009 (S8/E-801): a carry-forward credit note closing a source-year charge's remainder. Same cap check as
        /// <see cref="IssueCreditNoteAsync"/>; flagged <c>IsCarryForward</c> so the GL export skips it. Ambient (does NOT save).
        /// </summary>
        Task<CreditNote> IssueCarryForwardCreditNoteAsync(int chargeId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>BR-FEE-008: posted charges - credit notes - allocated payments, as of now.</summary>
        Task<decimal> ComputeStudentPositionAsync(int studentId, CancellationToken cancellationToken = default);
    }
}
