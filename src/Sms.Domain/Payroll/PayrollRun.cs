using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payroll
{
    /// <summary>
    /// ppl.PayrollRun — one month's salary run for the whole school (مسير الرواتب).
    /// <para>
    /// <b>A stated deviation from the specification, made at the owner's request on 2026-08-28.</b>
    /// doc/Modules/12 §2 puts "payroll calculation/payslips/WPS" out of scope (scope decision Q7)
    /// and BR-EMP-007 says in terms that "the SMS never computes net salary" — the module was
    /// specified to produce a *preparation export* and hand the arithmetic to whatever the school
    /// runs payroll on. docs/Future/02-Roadmap.md schedules the full payroll add-on for R3.
    /// The owner has asked for the calculation itself, and this is it. The gap is recorded here,
    /// in the commit that introduced it, and in <see cref="PayrollRunLine"/>, rather than being
    /// quietly presented as something the docs asked for.
    /// </para>
    /// <para>
    /// What is still *not* here, and is not silently substituted: no GL journal is posted for a run
    /// (the owner's call — statements first, the <c>IGlPostingPort</c> route later), no GOSI/WPS
    /// return, no end-of-service settlement, and no tax engine. The run computes what the school
    /// owes each employee this month and prints the four statements that go with it.
    /// </para>
    /// <para>
    /// The period is a calendar year and month, not an academic year, so the entity is
    /// deliberately <b>not</b> <see cref="IYearScoped"/>: salaries run through July and August
    /// whether or not a school year is open, and pinning a run to one would make the summer
    /// months unrepresentable. Not <see cref="IActivatable"/> either — like
    /// <c>fin.Receipt</c>, a money document carries a status and is cancelled, never deactivated.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T1)]
    public class PayrollRun : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>doc 08 PAY series — strict, because a payroll run is a money document and a gap in the sequence is a question somebody has to answer.</summary>
        public string PayrollRunNo { get; set; } = string.Empty;

        /// <summary>Gregorian year of the month being paid.</summary>
        public int PeriodYear { get; set; }

        /// <summary>1–12.</summary>
        public int PeriodMonth { get; set; }

        /// <summary>
        /// The date the salaries are paid — the school's intention while the run is open, and the
        /// date it actually happened once the run is marked paid. It is what the register and the
        /// bank transfer list are dated by.
        /// <para>
        /// Distinct from <see cref="PaidAtUtc"/>, which is when somebody sat down and recorded the
        /// payment. They differ whenever a month is closed a few days late, and the statements need
        /// the first while the audit trail needs the second.
        /// </para>
        /// </summary>
        public DateTime PaymentDate { get; set; }

        public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

        /// <summary>
        /// Cached totals over the run's lines, written whenever the lines change.
        /// <para>
        /// Stored rather than summed on read for a reason this codebase has already paid for:
        /// <c>SumAsync()</c> over a decimal column throws at runtime on Sqlite, so every list
        /// screen that wanted a total would have to materialise every line of every run to show
        /// it. Recomputed in one place (<c>PayrollAdmin</c>) so the cache cannot drift from a
        /// second writer.
        /// </para>
        /// </summary>
        public decimal TotalGross { get; set; }

        public decimal TotalDeductions { get; set; }

        public decimal TotalNet { get; set; }

        /// <summary>How many employee lines the run carries. Same reason as the totals.</summary>
        public int LineCount { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        public DateTime? PaidAtUtc { get; set; }

        public DateTime? CancelledAtUtc { get; set; }

        /// <summary>ملاحظات — why this run was cancelled, or anything the accountant wants the next reader to know.</summary>
        public string? Notes { get; set; }
    }
}
