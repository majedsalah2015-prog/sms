using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Sections;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    // ------------------------------------------------- Payments register (doc/Modules/21 §10, P-LIST)
    //
    // "What did we collect between these two dates, and for whose children." The cashier screen
    // answers one payer at a time and the allocation explorer answers one payer's history; neither
    // can answer a period, which is the question a finance office is actually asked — by the
    // principal at month end, by the auditor, and by the parent whose payment "never arrived".
    //
    // The awkward part of the question is that a receipt does not belong to a student. It belongs to
    // a payer (BR-FEE-004) and BR-PAY-003 spreads it oldest-first across everything that payer owes,
    // siblings included. So "the student's payment" is not a stored fact — it is what the allocation
    // engine assigned to that child's invoices, and the register reads it back that way rather than
    // inventing a student column the data does not have.

    /// <summary>
    /// doc/Modules/21 §10 "Receipt register" / "Daily collection report", P-LIST: receipts issued
    /// between two dates, attributed to the students their allocations paid for, filtered by
    /// student, guardian, grade and section.
    /// </summary>
    public sealed class PaymentRegisterViewModel : FinancePageViewModel
    {
        /// <summary>
        /// One receipt's money as it reached one student. A receipt allocated across two siblings
        /// is two rows and the two <see cref="Amount"/>s add back to the receipt; a receipt with
        /// money left over after the allocation carries a third row with no student, because the
        /// remainder is the family's credit balance (BR-PAY-003) and belongs to no child.
        /// </summary>
        public sealed record Row(
            Receipt Receipt,
            Parent? Payer,
            Student? Student,
            string? GradeName,
            string? SectionName,
            decimal Amount)
        {
            /// <summary>Money the engine could not put against an invoice — advance/credit on the payer, not on a child.</summary>
            public bool IsUnallocated => Student == null;

            /// <summary>BR-PAY-002 keeps a voided number in the series with its reason. It stays in the register and out of the totals.</summary>
            public bool IsVoid => Receipt.Status == ReceiptStatus.Void;
        }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>Inclusive, both ends — a receipt issued at any hour of <see cref="To"/> is in the register.</summary>
        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public string? Q { get; set; }

        public string? Guardian { get; set; }

        public int? GradeId { get; set; }

        public int? SectionId { get; set; }

        public IReadOnlyList<GradeLevel> Grades { get; set; } = Array.Empty<GradeLevel>();

        /// <summary>Sections of the chosen grade only; a school's full section list is meaningless as a flat picker.</summary>
        public IReadOnlyList<Section> Sections { get; set; } = Array.Empty<Section>();

        /// <summary>Rows the filters matched, before the display cap — so a truncated grid never reads as a complete one.</summary>
        public int MatchCount { get; set; }

        public bool IsTruncated { get; set; }

        /// <summary>Distinct receipts behind <see cref="MatchCount"/> — siblings on one receipt are one receipt, not two.</summary>
        public int ReceiptCount { get; set; }

        public int StudentCount { get; set; }

        /// <summary>Every matched row's money, voided receipts excluded. Computed over the whole match, not the displayed page.</summary>
        public decimal TotalCollected { get; set; }

        /// <summary>The part of it the engine left on the payer as credit (BR-PAY-003) — reported, never hidden inside the collected figure.</summary>
        public decimal TotalUnallocated { get; set; }

        public decimal TotalVoided { get; set; }

        /// <summary>BR-SEC-021: handing the school's collection out of the building is its own right.</summary>
        public bool CanExport { get; set; }

        /// <summary>
        /// Whether the receipt number is a link. The document itself is the cashier's screen, with
        /// the cashier's permission on it — BR-SEC-010 says an auditor who may read the register and
        /// not open a receipt is shown a number, not a link into a page that would 404 on them.
        /// </summary>
        public bool CanOpenReceipt { get; set; }

        /// <summary>
        /// True when a filter narrows the register to particular students. It changes what the totals
        /// mean — a student, grade or section filter can only match money that was allocated, so the
        /// unallocated remainder is not in them and the screen says so rather than letting the figure
        /// be read as the period's takings.
        /// </summary>
        public bool IsStudentFiltered => GradeId != null || SectionId != null || !string.IsNullOrWhiteSpace(Q);

        public bool HasAnyFilter => IsStudentFiltered || !string.IsNullOrWhiteSpace(Guardian);
    }
}
