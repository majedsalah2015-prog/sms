using System;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// Periodic tamper-evidence checkpoint (BR-AUD-007): a hash over a period's
    /// audit entries, chained to the previous checkpoint so storage-level edits
    /// or gaps are detectable. Append-only like the entries it covers.
    /// </summary>
    public class IntegrityCheckpoint
    {
        public long Id { get; set; }

        public DateTime PeriodStartUtc { get; set; }

        public DateTime PeriodEndUtc { get; set; }

        public long? FirstEntryId { get; set; }

        public long? LastEntryId { get; set; }

        public int EntryCount { get; set; }

        /// <summary>SHA-256 (hex) over the period's entries in id order.</summary>
        public string EntriesHash { get; set; } = string.Empty;

        public string? PreviousChainHash { get; set; }

        /// <summary>SHA-256 (hex) of previous chain hash + entries hash.</summary>
        public string ChainHash { get; set; } = string.Empty;

        public DateTime ComputedAtUtc { get; set; }
    }
}
