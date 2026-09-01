using System.Globalization;

namespace Sms.Application.Employees
{
    /// <summary>
    /// Reads a qualification's المعدل off a form without asking the reader's language what a
    /// decimal point is (doc/Modules/12 §8.2, BR-EMP-004; owner request 2026-08-27).
    /// <para>
    /// It exists because of a defect this screen shipped with for one afternoon: MVC binds a
    /// <c>decimal?</c> action parameter using <c>CurrentCulture</c>, the employee file runs under
    /// <c>ar-SA</c> half the time, and an <c>&lt;input type="number"&gt;</c> always posts the
    /// invariant form. Under Arabic "3.81" therefore failed to parse, bound as null, and the
    /// average vanished on save — with a success message on the screen. A number a registrar typed
    /// must never disappear quietly.
    /// </para>
    /// <para>
    /// Invariant first, because that is what the control sends; the reader's own culture second,
    /// for a value typed or pasted with a local separator. The culture is a parameter rather than
    /// ambient state so this stays a pure function and the Arabic case can be tested without a
    /// thread's culture being switched underneath it.
    /// </para>
    /// </summary>
    public static class GradePointAverageReader
    {
        /// <summary>
        /// The widest band that means anything here. The certificate states the average out of 4 or
        /// out of 100 and this product does not convert between them (see
        /// <c>Qualification.Gpa</c>), so the only bound worth enforcing is the column's own.
        /// </summary>
        public const decimal Maximum = 100m;

        /// <summary>
        /// True when <paramref name="raw"/> is blank (<paramref name="value"/> null — not recorded)
        /// or reads as a number in either culture. False means the registrar typed something that
        /// is not a number, which is a refusal rather than a silent drop.
        /// </summary>
        public static bool TryRead(string? raw, CultureInfo readerCulture, out decimal? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(raw)) { return true; }

            var text = raw.Trim();

            if (decimal.TryParse(text, Allowed, CultureInfo.InvariantCulture, out var invariant))
            {
                value = invariant;
                return true;
            }

            if (decimal.TryParse(text, Allowed, readerCulture, out var local))
            {
                value = local;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Deliberately without <see cref="NumberStyles.AllowThousands"/>, which
        /// <see cref="NumberStyles.Number"/> would have brought with it.
        /// <para>
        /// With thousands allowed, the invariant attempt reads "3,81" as three hundred and
        /// eighty-one — a comma is its thousands separator — and the value someone typed meaning
        /// 3.81 never reaches the second attempt that would have read it correctly. No average
        /// reaches a thousand on either scale, so the separator has nothing to separate here and
        /// only costs the reading its meaning.
        /// </para>
        /// </summary>
        private const NumberStyles Allowed =
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

        /// <summary>An unrecorded average is in range; a negative or out-of-100 one is not.</summary>
        public static bool IsInRange(decimal? value) => value == null || (value >= 0m && value <= Maximum);
    }
}
