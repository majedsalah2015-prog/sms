using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payroll
{
    /// <summary>
    /// ppl.SalaryAdvance — سلفة: money handed to an employee against future salary, recovered by
    /// instalment from the payroll runs that follow (owner request, 2026-08-28).
    /// <para>
    /// Nothing in doc/Modules/12 describes this. The nearest the specification comes is
    /// BR-EMP-008's offboarding clearance checklist, which lists "finance advances" as a thing to
    /// settle before an employee leaves — it assumed the advances lived in whatever system ran
    /// payroll. They live here now. See <see cref="PayrollRun"/> for the full statement of that
    /// deviation.
    /// </para>
    /// <para>
    /// T1 with a required reason on the amount: this is the school's money leaving on a promise,
    /// and the one edit nobody should be able to make quietly is the size of the promise. The
    /// attribute fires on <c>Modified</c> only, and advances are never pre-seeded as stubs, so the
    /// trap that caught <c>MarkEntry</c> does not apply here.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T1)]
    public class SalaryAdvance : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EmployeeId { get; set; }

        /// <summary>doc 08 ADV series.</summary>
        public string AdvanceNo { get; set; } = string.Empty;

        /// <summary>The date the employee asked, as the school records it — not the row's creation stamp, which is when somebody got round to typing it in.</summary>
        public DateTime RequestDate { get; set; }

        /// <summary>مبلغ السلفة.</summary>
        [RequiresAuditReason]
        public decimal Amount { get; set; }

        /// <summary>عدد الأقساط — how many monthly payroll runs it is recovered over.</summary>
        [RequiresAuditReason]
        public int InstallmentCount { get; set; }

        /// <summary>Gregorian year of the first month a deduction falls due.</summary>
        public int FirstDeductionYear { get; set; }

        /// <summary>1–12.</summary>
        public int FirstDeductionMonth { get; set; }

        /// <summary>سبب السلفة, in the employee's own words.</summary>
        public string? Reason { get; set; }

        public SalaryAdvanceStatus Status { get; set; } = SalaryAdvanceStatus.Requested;

        /// <summary>When the request was approved or rejected.</summary>
        public DateTime? DecisionAtUtc { get; set; }

        /// <summary>Why it was approved, rejected or cancelled. The one place a rejected employee's answer is written down.</summary>
        public string? DecisionNote { get; set; }

        /// <summary>The date the money actually reached the employee — the date the repayment schedule is built from.</summary>
        public DateTime? DisbursedOn { get; set; }

        public AdvanceDisbursementMethod? DisbursementMethod { get; set; }

        /// <summary>Transfer reference, cheque number, wallet transaction — whatever proves the money moved.</summary>
        public string? DisbursementRefNo { get; set; }

        /// <summary>When the last instalment was deducted or waived. Set by the payroll run, not by a person.</summary>
        public DateTime? SettledAtUtc { get; set; }
    }
}
