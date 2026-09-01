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
        /// <para>
        /// <paramref name="payer"/> is context for the entry's description and
        /// nothing else — see <see cref="GlBatchPayer"/>. It is a parameter rather
        /// than a field on the batch because it is not a fact the batch keeps: it
        /// is derived, at this moment, from the documents the batch already
        /// summarises.
        /// </para>
        /// </summary>
        Task<GlPostingOutcome> PostBatchAsync(GlExportBatch batch, GlBatchPayer payer, CancellationToken cancellationToken = default);

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
    /// Whose money a period's fee payments were, so the ledger entry can say so
    /// in the sentence an accountant reads.
    /// <para>
    /// The <b>lines</b> of a summary entry stay anonymous and always will —
    /// docs/Integration/01-Embedded-Accounting-Plan.md §8.2 rule 9 leaves
    /// <c>PartyType</c>/<c>PartyCode</c> empty, because a control-account total
    /// belongs to no one family and the school's own receivable subledger is the
    /// place that answers per student (§7.2). This is the other thing: prose on
    /// the entry header, which is how an accountant recognises "the Al-Ahmad
    /// payment" without opening the batch behind it.
    /// </para>
    /// <para>
    /// One student is named; several are only counted. Naming one family out of
    /// twelve would read as a statement about the entry, and it would be false.
    /// </para>
    /// </summary>
    /// <param name="StudentCount">Distinct students the period's fee payments were applied to — receipt → allocation → charge → student.</param>
    /// <param name="StudentNameAr">That student's Arabic name when <paramref name="StudentCount"/> is exactly one; null otherwise.</param>
    /// <param name="StudentNameEn">
    /// The English half. The embedded ERP's adapter uses only the Arabic one, because that ledger's
    /// own chart of accounts is Arabic — but this is the port, not that adapter, and a ledger
    /// attached to an English-speaking deployment would need the other half. Carrying both is also
    /// what BR-GLB-001 asks of any name this system hands out.
    /// </param>
    public sealed record GlBatchPayer(int StudentCount, string? StudentNameAr, string? StudentNameEn)
    {
        /// <summary>A period that collected nothing — or collected money nobody has applied to a charge yet, which is the same silence as far as a description goes.</summary>
        public static readonly GlBatchPayer None = new(0, null, null);
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
