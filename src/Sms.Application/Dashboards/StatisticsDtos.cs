using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Dashboards
{
    /// <summary>
    /// One named quantity in a breakdown — a grade's headcount, a fee category's
    /// billed total, a payment method's share.
    /// <para>
    /// Carries both languages because the name comes from data the school named
    /// (a grade level, a fee category), not from a string a view could translate.
    /// A screen that only carried one of them would be an Arabic page reading
    /// "Tuition" for the rest of the deployment's life.
    /// </para>
    /// </summary>
    public sealed record StatisticSlice(string NameEn, string NameAr, decimal Value);

    /// <summary>One calendar month carrying a single figure.</summary>
    public sealed record MonthlyPoint(int Year, int Month, decimal Value);

    /// <summary>
    /// One calendar month carrying two figures meant to be read against each
    /// other — billed against collected, revenue against expenses.
    /// </summary>
    public sealed record MonthlyPair(int Year, int Month, decimal First, decimal Second);

    /// <summary>
    /// Who is enrolled this year (doc/Modules/10 §11: "enrollment by status/stage").
    /// <para>
    /// Counted from <c>Enrollment</c> rather than <c>Student</c>: a student record
    /// outlives the year it was created in, so counting students would report
    /// every alumnus the school has ever had as if they were sitting in a
    /// classroom.
    /// </para>
    /// </summary>
    /// <param name="Total">Active enrollments in the selected year.</param>
    /// <param name="Male">Of <paramref name="Total"/>.</param>
    /// <param name="Female">Of <paramref name="Total"/>.</param>
    /// <param name="Admitted">Arrived through admissions this year, rather than rolled over.</param>
    /// <param name="Withdrawn">Enrollments that ended before the year did.</param>
    /// <param name="ByGrade">Headcount per grade level, in the grades' own teaching order.</param>
    /// <param name="ByStatus">Every enrollment in the year split by what became of it.</param>
    public sealed record StudentStatistics(
        int Total,
        int Male,
        int Female,
        int Admitted,
        int Withdrawn,
        IReadOnlyList<StatisticSlice> ByGrade,
        IReadOnlyList<StatisticSlice> ByStatus);

    /// <summary>
    /// Who teaches and who is on staff (doc/Modules/12 §11, doc/Modules/13 §8.4's
    /// load board reduced to its totals).
    /// </summary>
    /// <param name="Employees">Every employee on the books, whatever their status.</param>
    /// <param name="ActiveEmployees">Of those, currently active.</param>
    /// <param name="Teachers">Employees carrying a teaching profile.</param>
    /// <param name="AssignedTeachers">Of those, holding at least one assignment this year.</param>
    /// <param name="AveragePeriods">Mean assigned periods per teacher who has any.</param>
    /// <param name="StudentsPerTeacher">Active enrollments divided by assigned teachers; zero when nobody teaches.</param>
    /// <param name="ByStatus">Employees split by employment status.</param>
    /// <param name="ByLoadBand">Assigned teachers bucketed by how full their week is against their own cap.</param>
    public sealed record StaffStatistics(
        int Employees,
        int ActiveEmployees,
        int Teachers,
        int AssignedTeachers,
        decimal AveragePeriods,
        decimal StudentsPerTeacher,
        IReadOnlyList<StatisticSlice> ByStatus,
        IReadOnlyList<StatisticSlice> ByLoadBand);

    /// <summary>
    /// What the school billed this year (doc/Modules/19 §11).
    /// <para>
    /// <see cref="Outstanding"/> is the one figure here that is not a plain total:
    /// it comes from the fee module's own position calculator, so this screen and
    /// the receivables report cannot disagree about what is owed (BR-FEE-008,
    /// BR-DSH-002).
    /// </para>
    /// </summary>
    /// <param name="Billed">Gross posted charges — net plus VAT, voids excluded.</param>
    /// <param name="Net">The same charges before VAT.</param>
    /// <param name="Vat">Tax billed on top.</param>
    /// <param name="Discounts">Granted against those charges (doc/Modules/22).</param>
    /// <param name="CreditNotes">Issued against them (BR-FEE-011).</param>
    /// <param name="Outstanding">Still owed school-wide, per BR-FEE-008.</param>
    /// <param name="ChargeCount">Posted charges behind <paramref name="Billed"/>.</param>
    /// <param name="ByCategory">Gross billed per fee category, largest first.</param>
    /// <param name="ByMonth">Gross billed per month of the academic year.</param>
    public sealed record FeeStatistics(
        decimal Billed,
        decimal Net,
        decimal Vat,
        decimal Discounts,
        decimal CreditNotes,
        decimal Outstanding,
        int ChargeCount,
        IReadOnlyList<StatisticSlice> ByCategory,
        IReadOnlyList<MonthlyPoint> ByMonth)
    {
        /// <summary>
        /// Billed inside the year but posted outside its calendar months, so it
        /// appears in <see cref="Billed"/> and on no bar of <see cref="ByMonth"/>.
        /// <para>
        /// Not an anomaly — early registration bills the next year months before it
        /// opens, and that charge belongs to the year it is for, not the month it
        /// was raised in. But it means the monthly bars do not have to sum to the
        /// headline, and a chart that quietly totals less than the number printed
        /// above it is the exact failure BR-DSH-002 exists to prevent. Derived
        /// rather than counted separately, so the two can never drift; the screen
        /// prints it wherever it is non-zero.
        /// </para>
        /// </summary>
        public decimal BilledOutsideMonths => Billed - ByMonth.Sum(p => p.Value);
    }

    /// <summary>
    /// What came in against it (doc/Modules/21 §11).
    /// <para>
    /// Receipts carry no academic year of their own — a payment is dated, not
    /// enrolled — so every figure here is bounded by the selected year's calendar
    /// dates rather than by a year column. Voided receipts are excluded
    /// throughout: a cancelled receipt collected nothing.
    /// </para>
    /// </summary>
    /// <param name="Collected">Posted receipts issued inside the year's dates.</param>
    /// <param name="Refunded">
    /// Paid refund vouchers <em>raised</em> inside the year's dates. A refund
    /// records no payout date of its own, so raised-in-year is the closest the
    /// data supports — the screen says so where it shows the figure.
    /// </param>
    /// <param name="ReceiptCount">Posted receipts behind <paramref name="Collected"/>.</param>
    /// <param name="AverageReceipt">Mean posted receipt; zero when there are none.</param>
    /// <param name="CollectionRate">Collected as a percentage of billed; zero when nothing was billed.</param>
    /// <param name="ByMethod">Collected per payment method, largest first.</param>
    /// <param name="ByMonth">Billed against collected, month by month.</param>
    public sealed record PaymentStatistics(
        decimal Collected,
        decimal Refunded,
        int ReceiptCount,
        decimal AverageReceipt,
        decimal CollectionRate,
        IReadOnlyList<StatisticSlice> ByMethod,
        IReadOnlyList<MonthlyPair> ByMonth);

    /// <summary>
    /// What the school spent, read from the attached ledger through
    /// <c>IGlLedgerSummary</c>.
    /// <para>
    /// Absent — the whole record null, never a zeroed one — when no ledger is
    /// attached. "The school spent nothing" and "nobody asked the books" are
    /// different statements and the screen has to be able to tell them apart.
    /// </para>
    /// </summary>
    /// <param name="Revenue">Posted revenue over the year's dates, the ledger's own figure.</param>
    /// <param name="Expenses">Posted expenses over the same dates.</param>
    /// <param name="ByMonth">Revenue against expenses, month by month.</param>
    public sealed record ExpenseStatistics(
        decimal Revenue,
        decimal Expenses,
        IReadOnlyList<MonthlyPair> ByMonth)
    {
        /// <summary>Derived, so the surplus can never disagree with the two figures it comes from.</summary>
        public decimal Net => Revenue - Expenses;
    }

    /// <summary>
    /// The whole statistics screen in one snapshot, computed for one academic
    /// year (BR-DSH-005: the working year governs everything on it).
    /// </summary>
    /// <param name="Students">Never null.</param>
    /// <param name="Staff">Never null.</param>
    /// <param name="Fees">Never null.</param>
    /// <param name="Payments">Never null.</param>
    /// <param name="Expenses">Null when no ledger is attached — see <see cref="ExpenseStatistics"/>.</param>
    public sealed record SchoolStatistics(
        StudentStatistics Students,
        StaffStatistics Staff,
        FeeStatistics Fees,
        PaymentStatistics Payments,
        ExpenseStatistics? Expenses);
}
