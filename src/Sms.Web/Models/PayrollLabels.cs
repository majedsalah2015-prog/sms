using System.Globalization;
using Sms.Domain.Payroll;

namespace Sms.Web.Models
{
    /// <summary>
    /// Bilingual display text for everything the payroll and advances screens print out of an enum
    /// or a period number (owner request, 2026-08-28).
    /// <para>
    /// Its own file rather than a corner of <see cref="Labels"/>, following the convention that a
    /// module's labels live with the module. Nothing here calls <c>ToString()</c> on an enum: an
    /// Arabic register that prints "Disbursed" in the status column is a bilingual defect, not a
    /// cosmetic one.
    /// </para>
    /// </summary>
    public static class PayrollLabels
    {
        public static string RunStatus(PayrollRunStatus status, bool arabic) => status switch
        {
            PayrollRunStatus.Draft => arabic ? "مسودة" : "Draft",
            PayrollRunStatus.Approved => arabic ? "معتمد" : "Approved",
            PayrollRunStatus.Paid => arabic ? "مدفوع" : "Paid",
            PayrollRunStatus.Cancelled => arabic ? "ملغى" : "Cancelled",
            _ => status.ToString(),
        };

        /// <summary>The Bootstrap badge class a status wears, so one status is one colour product-wide.</summary>
        public static string RunStatusBadge(PayrollRunStatus status) => status switch
        {
            PayrollRunStatus.Draft => "bg-secondary",
            PayrollRunStatus.Approved => "bg-info text-dark",
            PayrollRunStatus.Paid => "bg-success",
            PayrollRunStatus.Cancelled => "bg-danger",
            _ => "bg-secondary",
        };

        public static string AdvanceStatus(SalaryAdvanceStatus status, bool arabic) => status switch
        {
            SalaryAdvanceStatus.Requested => arabic ? "مقدَّمة" : "Requested",
            SalaryAdvanceStatus.Approved => arabic ? "موافق عليها" : "Approved",
            SalaryAdvanceStatus.Rejected => arabic ? "مرفوضة" : "Rejected",
            SalaryAdvanceStatus.Disbursed => arabic ? "مصروفة" : "Disbursed",
            SalaryAdvanceStatus.Settled => arabic ? "مسدَّدة" : "Settled",
            SalaryAdvanceStatus.Cancelled => arabic ? "ملغاة" : "Cancelled",
            _ => status.ToString(),
        };

        public static string AdvanceStatusBadge(SalaryAdvanceStatus status) => status switch
        {
            SalaryAdvanceStatus.Requested => "bg-secondary",
            SalaryAdvanceStatus.Approved => "bg-info text-dark",
            SalaryAdvanceStatus.Disbursed => "bg-warning text-dark",
            SalaryAdvanceStatus.Settled => "bg-success",
            SalaryAdvanceStatus.Rejected or SalaryAdvanceStatus.Cancelled => "bg-danger",
            _ => "bg-secondary",
        };

        public static string InstallmentStatus(SalaryAdvanceInstallmentStatus status, bool arabic) => status switch
        {
            SalaryAdvanceInstallmentStatus.Scheduled => arabic ? "مجدول" : "Scheduled",
            SalaryAdvanceInstallmentStatus.Deducted => arabic ? "مستقطع" : "Deducted",
            SalaryAdvanceInstallmentStatus.Waived => arabic ? "معفى" : "Waived",
            _ => status.ToString(),
        };

        public static string InstallmentStatusBadge(SalaryAdvanceInstallmentStatus status) => status switch
        {
            SalaryAdvanceInstallmentStatus.Scheduled => "bg-secondary",
            SalaryAdvanceInstallmentStatus.Deducted => "bg-success",
            SalaryAdvanceInstallmentStatus.Waived => "bg-info text-dark",
            _ => "bg-secondary",
        };

        public static string DisbursementMethod(AdvanceDisbursementMethod method, bool arabic) => method switch
        {
            AdvanceDisbursementMethod.Cash => arabic ? "نقداً" : "Cash",
            AdvanceDisbursementMethod.BankTransfer => arabic ? "حوالة بنكية" : "Bank transfer",
            AdvanceDisbursementMethod.Cheque => arabic ? "شيك" : "Cheque",
            AdvanceDisbursementMethod.Wallet => arabic ? "محفظة إلكترونية" : "Mobile wallet",
            _ => method.ToString(),
        };

        public static string AdjustmentKind(PayrollAdjustmentKind kind, bool arabic) => kind switch
        {
            PayrollAdjustmentKind.Addition => arabic ? "إضافة" : "Addition",
            PayrollAdjustmentKind.Deduction => arabic ? "استقطاع" : "Deduction",
            _ => kind.ToString(),
        };

        /// <summary>
        /// A payroll period written the way a person says it — "سبتمبر 2026" / "September 2026".
        /// <para>
        /// The month name comes from the invariant Gregorian calendar and is translated here rather
        /// than read off <c>CultureInfo</c>. An ar-SA culture returns Hijri month names for a
        /// Gregorian month number, which would label a September payroll with a month it is not:
        /// CLAUDE.md's rule that dates are Gregorian with an optional Hijri sub-display, applied to
        /// the one place where getting it wrong renames the salary month itself.
        /// </para>
        /// </summary>
        public static string Period(int year, int month, bool arabic) =>
            $"{MonthName(month, arabic)} {year.ToString(CultureInfo.InvariantCulture)}";

        public static string MonthName(int month, bool arabic) => month switch
        {
            1 => arabic ? "يناير" : "January",
            2 => arabic ? "فبراير" : "February",
            3 => arabic ? "مارس" : "March",
            4 => arabic ? "أبريل" : "April",
            5 => arabic ? "مايو" : "May",
            6 => arabic ? "يونيو" : "June",
            7 => arabic ? "يوليو" : "July",
            8 => arabic ? "أغسطس" : "August",
            9 => arabic ? "سبتمبر" : "September",
            10 => arabic ? "أكتوبر" : "October",
            11 => arabic ? "نوفمبر" : "November",
            12 => arabic ? "ديسمبر" : "December",
            _ => month.ToString(CultureInfo.InvariantCulture),
        };

        /// <summary>
        /// Money, always with two decimals and always in Western digits.
        /// <para>
        /// Formatted against the invariant culture on purpose: CLAUDE.md fixes money as LTR-digit
        /// in both directions, and an ar-SA format would render Arabic-Indic numerals into a column
        /// a bank or an accountant has to read.
        /// </para>
        /// </summary>
        public static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
    }
}
