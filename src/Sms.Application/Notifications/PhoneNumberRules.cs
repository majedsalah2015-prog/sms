using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// Turns the number a registrar typed into the one a gateway will accept, or
    /// says why it cannot.
    /// <para>
    /// Every WhatsApp and SMS gateway wants E.164 — a leading <c>+</c>, a country
    /// code, no spaces, no dashes, no parentheses, no leading zero. Almost nothing in
    /// a school's parent register looks like that: <c>0599 123 456</c>,
    /// <c>+970-59-912-3456</c>, <c>00970599123456</c> and <c>٠٥٩٩١٢٣٤٥٦</c> are all
    /// the same person, and the last one is what an Arabic keyboard produces by
    /// default. Sending any of them verbatim earns a provider rejection per message,
    /// per parent, forever — so normalisation happens once, here, and the result is
    /// what gets snapshotted onto the delivery.
    /// </para>
    /// <para>
    /// <b>Arabic-Indic digits are converted, not rejected.</b> ٠١٢٣٤٥٦٧٨٩ and the
    /// Eastern variants ۰۱۲۳۴۵۶۷۸۹ both map to ASCII. A parent whose number was
    /// entered in Arabic numerals is not less reachable than one whose was not.
    /// </para>
    /// <para>
    /// <b>The local-format rule needs a country.</b> A number starting <c>0</c> is
    /// national-format and meaningless without knowing whose nation — so the caller
    /// passes the school's dialling code (BR-SET-004's country pack) and the leading
    /// zero is replaced by it. Without one, a local-format number is refused rather
    /// than guessed: a wrong country code delivers someone else's message to a
    /// stranger, which is worse than not sending.
    /// </para>
    /// </summary>
    public static class PhoneNumberRules
    {
        /// <summary>ITU-T E.164: at most 15 digits after the plus, and no real number is shorter than about 8.</summary>
        private const int MaxDigits = 15;

        private const int MinDigits = 8;

        /// <summary>Why a number could not be made sendable. The web boundary turns these into sentences.</summary>
        public enum Rejection
        {
            None = 0,

            /// <summary>Nothing was entered at all.</summary>
            Empty,

            /// <summary>Something is in the field, but no digits are.</summary>
            NoDigits,

            /// <summary>National format (a leading zero, or too short to be international) and no dialling code was supplied to complete it.</summary>
            NeedsCountryCode,

            /// <summary>Fewer digits than any real subscriber number.</summary>
            TooShort,

            /// <summary>More than E.164's fifteen digits.</summary>
            TooLong,
        }

        public readonly struct Result
        {
            private Result(string? e164, Rejection rejection)
            {
                E164 = e164;
                Rejection = rejection;
            }

            /// <summary>The sendable number, <c>+</c> included. Null unless <see cref="IsValid"/>.</summary>
            public string? E164 { get; }

            public Rejection Rejection { get; }

            public bool IsValid => Rejection == Rejection.None;

            internal static Result Ok(string e164) => new(e164, Rejection.None);

            internal static Result No(Rejection rejection) => new(null, rejection);
        }

        /// <summary>
        /// The sendable form of <paramref name="entered"/>.
        /// <paramref name="defaultDiallingCode"/> is the school's country code — with or
        /// without its plus, e.g. "970" or "+966" — and is used only to complete a
        /// national-format number.
        /// </summary>
        public static Result Normalize(string? entered, string? defaultDiallingCode = null)
        {
            if (string.IsNullOrWhiteSpace(entered))
            {
                return Result.No(Rejection.Empty);
            }

            var text = ToAsciiDigits(entered.Trim());

            // A plus is only a plus at the front; "+" mid-number is punctuation someone
            // typed, and 00 is the same intention dialled the old way.
            var isInternational = text.StartsWith("+", StringComparison.Ordinal);
            if (!isInternational && text.StartsWith("00", StringComparison.Ordinal))
            {
                isInternational = true;
                text = text.Substring(2);
            }

            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                return Result.No(Rejection.NoDigits);
            }

            if (!isInternational)
            {
                // A leading zero is the national trunk prefix: drop it and prepend the
                // country. Anything else with no plus is ambiguous — it might already
                // carry a country code, it might not — so it is only accepted as-is when
                // it is too long to be a local number and no dialling code was offered.
                var code = DiallingDigits(defaultDiallingCode);

                if (digits.StartsWith("0", StringComparison.Ordinal))
                {
                    if (code == null)
                    {
                        return Result.No(Rejection.NeedsCountryCode);
                    }

                    digits = code + digits.TrimStart('0');
                }
                else if (code != null && !digits.StartsWith(code, StringComparison.Ordinal))
                {
                    digits = code + digits;
                }
            }

            if (digits.Length < MinDigits)
            {
                return Result.No(Rejection.TooShort);
            }

            return digits.Length > MaxDigits
                ? Result.No(Rejection.TooLong)
                : Result.Ok("+" + digits);
        }

        /// <summary>Whether <paramref name="entered"/> can be sent to at all — the cheap check a screen makes while a clerk types.</summary>
        public static bool IsSendable(string? entered, string? defaultDiallingCode = null)
            => Normalize(entered, defaultDiallingCode).IsValid;

        /// <summary>
        /// The number with all but its last four digits hidden, for a screen that must
        /// show a delivery went somewhere without publishing a parent's mobile to
        /// whoever can open the operations log.
        /// </summary>
        public static string Mask(string? e164)
        {
            if (string.IsNullOrWhiteSpace(e164))
            {
                return string.Empty;
            }

            var digits = new string(e164.Where(char.IsDigit).ToArray());
            return digits.Length <= 4
                ? new string('•', digits.Length)
                : new string('•', digits.Length - 4) + digits.Substring(digits.Length - 4);
        }

        private static string? DiallingDigits(string? diallingCode)
        {
            if (string.IsNullOrWhiteSpace(diallingCode))
            {
                return null;
            }

            var digits = new string(ToAsciiDigits(diallingCode).Where(char.IsDigit).ToArray());
            return digits.Length == 0 ? null : digits;
        }

        /// <summary>
        /// Arabic-Indic (٠-٩) and Eastern Arabic-Indic (۰-۹) digits to ASCII. Unicode
        /// gives both ranges the numeric values 0..9, so the conversion is the digit's
        /// own value rather than a lookup table that could drift.
        /// </summary>
        private static string ToAsciiDigits(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                builder.Append(char.IsDigit(character) && character > '9'
                    ? (char)('0' + CharUnicodeInfo.GetDecimalDigitValue(character))
                    : character);
            }

            return builder.ToString();
        }
    }
}
