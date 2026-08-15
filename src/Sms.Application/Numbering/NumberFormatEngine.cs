using System.Text.RegularExpressions;

namespace Sms.Application.Numbering
{
    /// <summary>
    /// Pure doc 08 §2 template rendering. {PREFIX} and {SEP} are not runtime
    /// tokens — a series' literal prefix/separator characters are simply
    /// typed into the template (e.g. "STU-{YEAR}-{SEQ:5}"); only
    /// {SCHOOL}/{YEAR}/{GYEAR}/{SEQ:n} substitute. Output is always the
    /// invariant Latin-digit canonical form (BR-NUM-007) — Arabic-Indic
    /// display conversion is a presentation concern, not this engine's.
    /// </summary>
    public static class NumberFormatEngine
    {
        private static readonly Regex SequenceToken = new(@"\{SEQ:(\d+)\}", RegexOptions.Compiled);

        public static string Render(string template, NumberFormatContext context)
        {
            var rendered = template
                .Replace("{SCHOOL}", context.SchoolCode)
                .Replace("{YEAR}", context.AcademicYearLabel)
                .Replace("{GYEAR}", context.GregorianYear.ToString());

            return SequenceToken.Replace(rendered, match =>
            {
                var digits = int.Parse(match.Groups[1].Value);
                return context.Sequence.ToString().PadLeft(digits, '0');
            });
        }
    }
}
