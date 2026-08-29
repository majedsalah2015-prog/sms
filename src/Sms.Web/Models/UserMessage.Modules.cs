using System;
using System.Globalization;

namespace Sms.Web.Models
{
    /// <summary>
    /// The module refusal tables, and the order they are asked in.
    /// <para>
    /// One table per product area rather than one switch for the whole product. The tables are
    /// disjoint — an exception type appears in exactly one — so the order is a reading order, not a
    /// precedence rule, and a module's refusals can be written, reviewed and argued about without
    /// opening a file that also holds the cafeteria's.
    /// </para>
    /// <para>
    /// Each table returns <c>null</c> for what it does not own, so the chain falls through to the
    /// next one and finally to the engine's own English sentence. <c>RefusalCoverageTests</c> asserts
    /// that last step is unreachable: every exception type the product defines is named by exactly
    /// one table, and a new one fails the build until it is.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        private static string? ByModule(Exception exception, bool arabic)
            => People(exception, arabic)
            ?? Finance(exception, arabic)
            ?? Payroll(exception, arabic)
            ?? Learning(exception, arabic)
            ?? Services(exception, arabic)
            ?? Platform(exception, arabic);

        /// <summary>
        /// A number as a person reads it, in Latin digits in both languages.
        /// <para>
        /// Money and counts stay LTR-digit here for the same reason they do on every screen: an
        /// amount is checked against a receipt, and a receipt does not change its digits with the
        /// interface language.
        /// </para>
        /// </summary>
        private static string Amount(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

        /// <summary>A count in the sentence, Latin-digit for the same reason as <see cref="Amount(decimal)"/>.</summary>
        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>A date the reader can compare against the one they typed. Gregorian, as every date entry here is.</summary>
        private static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>A term or a semester, named as the calendar screens name it.</summary>
        private static string PeriodName(Sms.Application.Common.Exceptions.SchoolPeriodKind kind, bool arabic)
            => kind == Sms.Application.Common.Exceptions.SchoolPeriodKind.Term
                ? (arabic ? "الفترة" : "term")
                : (arabic ? "الفصل الدراسي" : "semester");
    }
}
