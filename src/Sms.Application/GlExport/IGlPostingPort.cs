using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.GlExport;

namespace Sms.Application.GlExport
{
    /// <summary>
    /// Hands a generated journal batch to a general ledger. The port the O3
    /// deferral left open — E-503 built the journal composition and stopped at a
    /// CSV file because, at the time, nobody knew which accounting system the
    /// pilot school ran (Implementation 01 O3, and the comment on
    /// <see cref="IGlExportService"/> saying as much).
    /// <para>
    /// <b>Optional by design.</b> When no implementation is registered — a school
    /// deployed without the embedded accounting — <see cref="IGlExportService"/>
    /// behaves exactly as it always has: the batch is generated, balanced,
    /// numbered and renderable as CSV, and nothing is posted. That is the
    /// fallback, not a degraded mode.
    /// </para>
    /// <para>
    /// The batch is the unit, not the document. Charges, receipts and the rest
    /// fold into summary lines before they arrive here, which is the deliberate
    /// choice of docs/Integration/01-Embedded-Accounting-Plan.md §7.2: the
    /// per-student receivable ledger is this system — statements, ageing,
    /// dunning, the parent portal — and duplicating it in the GL would produce a
    /// second, worse copy of a subsidiary ledger that already exists.
    /// </para>
    /// </summary>
    public interface IGlPostingPort
    {
        /// <summary>
        /// Posts the batch's lines as one balanced journal entry and returns the
        /// ledger's own document number.
        /// <para>
        /// Must be idempotent per batch: a repeated call for a batch already
        /// posted reports the existing entry rather than creating a second one.
        /// The batch number is the natural key for that, since a batch's period
        /// may not overlap another's.
        /// </para>
        /// </summary>
        Task<GlPostingOutcome> PostBatchAsync(GlExportBatch batch, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reverses a posted batch, so its period can be regenerated after
        /// corrections. Called when the batch is voided.
        /// <para>
        /// A reversal is a second entry with the sides swapped, never an edit or a
        /// delete: a posted ledger entry is immutable, and the pair has to remain
        /// visible for the correction to be auditable at all.
        /// </para>
        /// </summary>
        Task<GlPostingOutcome> ReverseBatchAsync(GlExportBatch batch, string reason, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// What the ledger said. Failure is reported rather than thrown because the
    /// caller has a real choice to make with it — most refusals (a closed period,
    /// an account that is not postable) are configuration to fix and retry, not
    /// faults.
    /// </summary>
    public sealed class GlPostingOutcome
    {
        private GlPostingOutcome(bool success, string? documentNumber, string? errorCode, string? errorMessage)
        {
            Success = success;
            DocumentNumber = documentNumber;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }

        /// <summary>The ledger's document number, e.g. <c>SY-2026-000042</c>. Present only on success.</summary>
        public string? DocumentNumber { get; }

        public string? ErrorCode { get; }

        public string? ErrorMessage { get; }

        public static GlPostingOutcome Ok(string documentNumber) => new(true, documentNumber, null, null);

        public static GlPostingOutcome Failed(string errorCode, string errorMessage) => new(false, null, errorCode, errorMessage);
    }
}
