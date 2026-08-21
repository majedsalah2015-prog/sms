using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.GlExport
{
    /// <summary>
    /// One generated journal-summary export covering a closed period:
    /// numbered ("GLX"), balanced (TotalDebit == TotalCredit, guaranteed by
    /// JournalSummaryBuilder), with a SHA-256 over the rendered CSV so a
    /// re-render can be verified against what was handed to accounting.
    /// Periods may not overlap an existing non-voided batch — the same
    /// documents must never reach the ledger twice.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class GlExportBatch : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public DateTime PeriodFromUtc { get; set; }

        public DateTime PeriodToUtc { get; set; }

        public DateTime GeneratedAtUtc { get; set; }

        public int GeneratedByUserId { get; set; }

        public decimal TotalDebit { get; set; }

        public decimal TotalCredit { get; set; }

        public int SourceDocumentCount { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public GlExportBatchStatus Status { get; set; } = GlExportBatchStatus.Generated;

        /// <summary>
        /// The general ledger's own document number for this batch, once it has
        /// been posted through <c>IGlPostingPort</c> — e.g. <c>SY-2026-000042</c>.
        /// Null when no ledger is attached to this deployment, which is the O3
        /// fallback: the batch is still generated, balanced and exportable as CSV.
        /// <para>
        /// Kept here rather than inferred, because it is the only thread back from
        /// a school period to the entry an accountant is looking at.
        /// </para>
        /// </summary>
        public string? PostedJournalNo { get; set; }

        /// <summary>The ledger's document number for the reversing entry, once the batch has been voided against a ledger. Null otherwise.</summary>
        public string? ReversalJournalNo { get; set; }

        [RequiresAuditReason]
        public string? VoidReason { get; set; }

        public List<GlJournalLine> Lines { get; set; } = new();
    }

    public enum GlExportBatchStatus : short
    {
        Generated = 1,
        Voided = 2,
    }
}
