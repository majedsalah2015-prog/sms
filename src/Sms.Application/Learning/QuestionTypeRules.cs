using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure BR-LRN-011: what each question type must look like to be answerable,
    /// and how an objective answer is judged.
    ///
    /// <para>
    /// Both halves live here rather than in a service because both are decisions,
    /// not data access — and because the marking half will one day run twice: once
    /// when a sitting is auto-marked, and once when a teacher previews what a
    /// question will accept. Two copies of "is this answer right" is how a school
    /// ends up with a preview that disagrees with the mark.
    /// </para>
    /// </summary>
    public static class QuestionTypeRules
    {
        /// <summary>
        /// BR-LRN-011's line: everything up to and including
        /// <see cref="QuestionType.ShortText"/> marks itself; anything after it is
        /// a constructed response for the manual queue. Read off the enum's order
        /// so a new objective type is one member and one number, and a new
        /// constructed one cannot land on the wrong side by accident.
        /// </summary>
        public static bool IsAutoMarkable(QuestionType type)
            => (int)type <= (int)QuestionType.ShortText;

        public static bool RequiresOptions(QuestionType type)
            => type is QuestionType.SingleChoice or QuestionType.MultipleChoice or QuestionType.TrueFalse;

        /// <summary>Exactly one option may be correct — the type's whole meaning.</summary>
        public static bool RequiresExactlyOneCorrect(QuestionType type)
            => type is QuestionType.SingleChoice or QuestionType.TrueFalse;

        public static bool RequiresAcceptedAnswers(QuestionType type)
            => type is QuestionType.Numeric or QuestionType.ShortText;

        public static bool AllowsTolerance(QuestionType type)
            => type == QuestionType.Numeric;

        /// <summary>
        /// The first refusal that applies, or <see cref="QuestionShapeRefusal.None"/>.
        /// Ordered so the author fixes what the question <em>is</em> before what it
        /// is worth.
        /// </summary>
        public static QuestionShapeRefusal Check(
            QuestionType type,
            decimal marks,
            IReadOnlyCollection<bool> optionCorrectness,
            IReadOnlyCollection<string> acceptedAnswers,
            decimal? numericTolerance)
        {
            optionCorrectness ??= Array.Empty<bool>();
            acceptedAnswers ??= Array.Empty<string>();

            if (RequiresOptions(type))
            {
                // One option is not a choice, it is an instruction.
                if (optionCorrectness.Count < 2)
                {
                    return QuestionShapeRefusal.TooFewOptions;
                }

                var correct = optionCorrectness.Count(c => c);

                if (correct == 0)
                {
                    return QuestionShapeRefusal.NoCorrectOption;
                }

                if (RequiresExactlyOneCorrect(type) && correct > 1)
                {
                    return QuestionShapeRefusal.TooManyCorrectOptions;
                }

                // Every option correct is not a question. It marks the whole class
                // right whatever they pick, which reads as generosity and is
                // actually a broken item hiding in the analytics.
                if (type == QuestionType.MultipleChoice && correct == optionCorrectness.Count)
                {
                    return QuestionShapeRefusal.EveryOptionCorrect;
                }
            }
            else if (optionCorrectness.Count > 0)
            {
                return QuestionShapeRefusal.OptionsOnANonChoiceType;
            }

            if (RequiresAcceptedAnswers(type))
            {
                if (acceptedAnswers.Count == 0 || acceptedAnswers.All(string.IsNullOrWhiteSpace))
                {
                    return QuestionShapeRefusal.NoAcceptedAnswer;
                }

                if (type == QuestionType.Numeric && acceptedAnswers.Any(a => !TryParseNumber(a, out _)))
                {
                    return QuestionShapeRefusal.NonNumericAcceptedAnswer;
                }
            }
            else if (acceptedAnswers.Count > 0)
            {
                return QuestionShapeRefusal.AcceptedAnswersOnAChoiceType;
            }

            if (numericTolerance is decimal tolerance)
            {
                if (!AllowsTolerance(type))
                {
                    return QuestionShapeRefusal.ToleranceOnANonNumericType;
                }

                if (tolerance < 0m)
                {
                    return QuestionShapeRefusal.NegativeTolerance;
                }
            }

            // Last, because a question worth nothing is a real thing to want while
            // drafting and only wrong once it is asked.
            if (marks <= 0m)
            {
                return QuestionShapeRefusal.MarksNotPositive;
            }

            return QuestionShapeRefusal.None;
        }

        // ---------------------------------------------------------------- marking

        /// <summary>
        /// BR-LRN-011 numeric: right when it lands within the author's stated
        /// tolerance. A null tolerance is exact — and so is a zero one, which is a
        /// different statement the author is allowed to make.
        /// </summary>
        public static bool MatchesNumeric(decimal expected, decimal? tolerance, string? given)
        {
            if (!TryParseNumber(given, out var value))
            {
                return false;
            }

            return Math.Abs(value - expected) <= (tolerance ?? 0m);
        }

        /// <summary>
        /// BR-LRN-011 exact-match short text — "exact" after the normalisation
        /// below, which is the difference between marking a paper and punishing
        /// typing.
        ///
        /// <para>
        /// Case and surrounding or repeated whitespace never carried meaning in an
        /// answer box. Arabic needs two more: the diacritics a student may or may
        /// not type (<c>مَاء</c> and <c>ماء</c> are one word), and the alef and
        /// yaa forms that most keyboards make interchangeable (<c>إجابة</c>,
        /// <c>أجابة</c>, <c>اجابة</c>). Marking those wrong is not strictness, it
        /// is a defect that falls on exactly the students least fluent with a
        /// keyboard.
        /// </para>
        /// </summary>
        public static bool MatchesShortText(IReadOnlyCollection<string> accepted, string? given)
        {
            if (accepted == null || accepted.Count == 0 || string.IsNullOrWhiteSpace(given))
            {
                return false;
            }

            var normalised = NormaliseAnswer(given);

            return accepted.Any(a => !string.IsNullOrWhiteSpace(a)
                && string.Equals(NormaliseAnswer(a), normalised, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Exposed so a screen can show an author what their accepted answers
        /// actually compare as. An author who cannot see the normalisation will
        /// eventually list the same answer three times to be safe.
        /// </summary>
        public static string NormaliseAnswer(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            var lastWasSpace = false;

            foreach (var c in text.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        builder.Append(' ');
                        lastWasSpace = true;
                    }

                    continue;
                }

                lastWasSpace = false;

                // Arabic diacritics (U+064B..U+0652), the superscript alef and the
                // tatweel: typed by some students, by no keyboards' default, and
                // never the difference between two answers.
                if ((c >= 'ً' && c <= 'ْ') || c == 'ٰ' || c == 'ـ')
                {
                    continue;
                }

                builder.Append(c switch
                {
                    'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', // أ إ آ ٱ -> ا
                    'ى' => 'ي',                                     // ى -> ي
                    'ة' => 'ه',                                     // ة -> ه
                    _ => c,
                });
            }

            return builder.ToString();
        }

        private static bool TryParseNumber(string? text, out decimal value)
            => decimal.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
    }

    /// <summary>
    /// Why a question cannot be asked as it stands. A code rather than a sentence,
    /// so the Web boundary translates it and the engine stays language-free.
    /// </summary>
    public enum QuestionShapeRefusal
    {
        None = 0,
        TooFewOptions = 1,
        NoCorrectOption = 2,
        TooManyCorrectOptions = 3,
        EveryOptionCorrect = 4,
        OptionsOnANonChoiceType = 5,
        NoAcceptedAnswer = 6,
        NonNumericAcceptedAnswer = 7,
        AcceptedAnswersOnAChoiceType = 8,
        ToleranceOnANonNumericType = 9,
        NegativeTolerance = 10,
        MarksNotPositive = 11,
    }
}
