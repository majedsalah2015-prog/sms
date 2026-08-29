using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payroll
{
    /// <summary>
    /// ppl.SalaryAdvanceInstallment — one month of one advance's repayment schedule (قسط السلفة).
    /// <para>
    /// Built in full when the advance is disbursed, so the employee is told the whole schedule at
    /// the moment they take the money rather than discovering it a month at a time. Each row is
    /// then consumed by the payroll run for its month.
    /// </para>
    /// <para>
    /// <see cref="PayrollRunLineId"/> is the link that makes the advances statement answerable:
    /// without it "deducted" is a claim, and with it every recovered riyal names the payslip that
    /// recovered it.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T2)]
    public class SalaryAdvanceInstallment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int SalaryAdvanceId { get; set; }

        /// <summary>1..N, in due order.</summary>
        public int SequenceNo { get; set; }

        /// <summary>Gregorian year of the payroll month this falls due in.</summary>
        public int DueYear { get; set; }

        /// <summary>1–12.</summary>
        public int DueMonth { get; set; }

        public decimal Amount { get; set; }

        public SalaryAdvanceInstallmentStatus Status { get; set; } = SalaryAdvanceInstallmentStatus.Scheduled;

        /// <summary>The payslip that recovered it. Null while scheduled, and null forever if waived.</summary>
        public int? PayrollRunLineId { get; set; }

        /// <summary>When the run carrying it was marked paid.</summary>
        public DateTime? DeductedAtUtc { get; set; }

        /// <summary>Why the school forgave this instalment. Required in practice by the screen, not by the column — an old import has neither.</summary>
        public string? WaiverNote { get; set; }
    }
}
