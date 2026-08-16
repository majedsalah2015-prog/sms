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

        Task<FeeStructureLine> DefineStructureLineAsync(
            int gradeYearProfileId, int feeCategoryId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidFeeStructureLineStatusTransitionException"/>.</summary>
        Task ApproveStructureLineAsync(int feeStructureLineId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.FeeStructureLineNotApprovedException"/> when no approved line covers the student's grade-year x category.</summary>
        Task<Charge> PostChargeAsync(
            int studentId, int payerId, int gradeYearProfileId, int feeCategoryId, ChargeSourceType sourceType,
            CancellationToken cancellationToken = default);

        /// <summary>BR-FEE-003: manual/misc charges post an explicit amount rather than reading a structure line.</summary>
        Task<Charge> PostManualChargeAsync(
            int studentId, int payerId, int feeCategoryId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.ChargeNotPostedException"/> or <see cref="Common.Exceptions.CreditNoteExceedsChargeException"/>.</summary>
        Task<CreditNote> IssueCreditNoteAsync(int chargeId, decimal amount, string reason, CancellationToken cancellationToken = default);

        /// <summary>BR-FEE-008: posted charges - credit notes - allocated payments, as of now.</summary>
        Task<decimal> ComputeStudentPositionAsync(int studentId, CancellationToken cancellationToken = default);
    }
}
