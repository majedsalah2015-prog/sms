using System;
using System.Collections.Generic;

namespace Sms.Web.Api.Models
{
    /// <summary>
    /// Whether a ledger is attached to this deployment. The app reads this once
    /// and hides its accounting section rather than showing one whose every
    /// endpoint answers 503.
    /// </summary>
    public sealed class ApiLedgerStatus
    {
        /// <summary>False on a standalone school system — the bridge is not registered.</summary>
        public bool IsAttached { get; set; }

        /// <summary>Whether the revenue/expense summary is available as well as the chart and entries.</summary>
        public bool SupportsResultSummary { get; set; }
    }

    /// <summary>One postable account. Codes and names only — never a surrogate id from the other product's tables.</summary>
    public sealed class ApiGlAccount
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Asset / Liability / Equity / Revenue / Expense, or Unspecified when the ledger did not say.</summary>
        public string Nature { get; set; } = string.Empty;
    }

    /// <summary>
    /// The trial balance's two column totals as of a date, without its rows.
    /// </summary>
    public sealed class ApiTrialBalance
    {
        public DateTime AsOf { get; set; }

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }

        /// <summary>Signed: positive when the debit side is the heavier one.</summary>
        public decimal Difference { get; set; }

        /// <summary>
        /// Reported, not asserted. Double entry means it must be true; showing it
        /// lets a reader trust the figure without going to look for the test.
        /// </summary>
        public bool IsBalanced { get; set; }

        /// <summary>Posting accounts carrying a non-zero balance.</summary>
        public int AccountCount { get; set; }

        public string Currency { get; set; } = string.Empty;
    }

    /// <summary>One journal entry as a list shows it.</summary>
    public sealed class ApiGlEntry
    {
        public string? Number { get; set; }

        public DateTime EntryDate { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? Reference { get; set; }

        /// <summary>Which module raised it — the school's fee batch, a purchase, a till close.</summary>
        public string? SourceModule { get; set; }

        /// <summary>The entry's total debit: for a balanced entry, its size.</summary>
        public decimal Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        /// <summary>Draft / Posted / Reversed, or Unspecified for a state this system does not recognise.</summary>
        public string State { get; set; } = string.Empty;

        public string? CreatedBy { get; set; }
    }

    /// <summary>
    /// What the books say the school earned and spent. Both are positive
    /// magnitudes in their natural direction, so a client subtracts one from the
    /// other without knowing which side each normally sits on.
    /// </summary>
    public sealed class ApiLedgerResult
    {
        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public string Currency { get; set; } = string.Empty;

        public decimal Revenue { get; set; }

        public decimal Expenses { get; set; }

        /// <summary>Derived. A surplus is what is left over, not a third figure that could disagree.</summary>
        public decimal Net { get; set; }

        /// <summary>Oldest first. A quiet month comes back as zero rather than missing.</summary>
        public IReadOnlyList<ApiLedgerMonth> Months { get; set; } = Array.Empty<ApiLedgerMonth>();
    }

    /// <summary>One calendar month of <see cref="ApiLedgerResult"/>.</summary>
    public sealed class ApiLedgerMonth
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public decimal Revenue { get; set; }

        public decimal Expenses { get; set; }

        public decimal Net { get; set; }
    }
}
