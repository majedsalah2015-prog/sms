using System;
using System.Collections.Generic;
using Sms.Domain.GlExport;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- GL export (doc/Modules/19 §8)

    public sealed class GlExportIndexViewModel
    {
        public sealed record BatchRow(
            int Id, string BatchNo, DateTime PeriodFromUtc, DateTime PeriodToUtc,
            decimal TotalDebit, decimal TotalCredit, int SourceDocumentCount,
            GlExportBatchStatus Status, string? PostedJournalNo, DateTime GeneratedAtUtc);

        public IReadOnlyList<BatchRow> Batches { get; set; } = Array.Empty<BatchRow>();

        /// <summary>True when an <c>IGlPostingPort</c> is registered. False is not a fault: the batch is still generated, balanced and downloadable as CSV (the O3 fallback).</summary>
        public bool LedgerAttached { get; set; }

        public int MappedKeyCount { get; set; }

        public int UnmappedKeyCount { get; set; }

        public DateTime PeriodFrom { get; set; }

        public DateTime PeriodTo { get; set; }
    }

    public sealed class GlExportBatchViewModel
    {
        public GlExportBatch Batch { get; set; } = null!;

        public IReadOnlyList<GlJournalLine> Lines { get; set; } = Array.Empty<GlJournalLine>();

        /// <summary>Account code → the name it was mapped under, in the reader's language. Missing when a code was mapped and the mapping later changed — the line keeps the code it posted with.</summary>
        public IReadOnlyDictionary<string, string> AccountNames { get; set; } = new Dictionary<string, string>();

        public bool LedgerAttached { get; set; }
    }

    public sealed class GlMappingsViewModel
    {
        public IReadOnlyList<GlAccountMapping> Rows { get; set; } = Array.Empty<GlAccountMapping>();

        /// <summary>Keys a batch could need that have no account yet. Every one of these is a period that will refuse to generate.</summary>
        public IReadOnlyList<string> UnmappedKeys { get; set; } = Array.Empty<string>();

        public bool LedgerAttached { get; set; }
    }
}
