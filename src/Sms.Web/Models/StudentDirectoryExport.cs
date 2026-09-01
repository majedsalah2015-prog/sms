using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sms.Web.Models
{
    /// <summary>
    /// Turning the student directory into a file somebody opens somewhere else
    /// (doc/Modules/10 §8 — "student search/list (global, filters, saved views,
    /// export-gated)"), and into the sentence that says what the file holds.
    /// <para>
    /// Separated from the controller because both halves are exactly the sort of
    /// thing that is wrong in a way nobody notices: a name containing a comma
    /// shifts every column after it, an Arabic name without a byte-order mark
    /// arrives in Excel as mojibake, and a filter description that quietly falls
    /// back to English is a bilingual defect on a document a school keeps. Each
    /// is pinned by a test rather than by a reading.
    /// </para>
    /// </summary>
    public static class StudentDirectoryExport
    {
        /// <summary>
        /// One CSV cell. Always quoted, with the quote itself doubled — a family name can carry a
        /// comma, an address line usually does, and quoting only the cells that look dangerous
        /// means the escaping is decided by whoever typed the name.
        /// </summary>
        public static string Cell(string? value)
            => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        /// <summary>One CSV record, comma-separated, every cell quoted.</summary>
        public static string Line(IEnumerable<string?> cells)
            => string.Join(",", (cells ?? Array.Empty<string?>()).Select(Cell));

        /// <summary>
        /// The finished file. UTF-8 with a byte-order mark, because the first thing anybody does
        /// with this download is open it in Excel, and Excel without the mark reads every Arabic
        /// name as mojibake — which looks like the system mangled the register rather than like a
        /// missing three bytes.
        /// </summary>
        public static byte[] Bytes(IEnumerable<IEnumerable<string?>> records)
        {
            var text = new StringBuilder();
            foreach (var record in records ?? Array.Empty<IEnumerable<string?>>())
            {
                text.Append(Line(record)).Append("\r\n");
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
        }

        /// <summary>
        /// The column headings, in the reader's language — the file is read by the person who
        /// asked for it, not by a machine, so the headings follow the screen rather than staying
        /// English for the sake of a stable schema.
        /// </summary>
        public static IReadOnlyList<string> Headings(bool arabic) => new[]
        {
            arabic ? "رقم الطالب" : "Student no.",
            arabic ? "الاسم" : "Name",
            arabic ? "الجنس" : "Gender",
            arabic ? "تاريخ الميلاد" : "Date of birth",
            arabic ? "الصف" : "Grade",
            arabic ? "الشعبة" : "Section",
            arabic ? "ولي الأمر" : "Primary parent",
            arabic ? "الجوال" : "Mobile",
            arabic ? "الحالة" : "Status",
        };

        /// <summary>
        /// What the export and the printed sheet were filtered to, as short phrases in the
        /// reader's language. An empty list means the whole register, and both surfaces say so
        /// rather than leaving the question open.
        /// <para>
        /// Grade and section arrive already resolved to names: which of the two languages a
        /// <c>GradeLevel</c> shows is the caller's business, and this class does not hold a
        /// database.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> Describe(
            bool arabic, string? query, string? status, string? grade, string? section, string? gender)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(grade))
            {
                parts.Add((arabic ? "الصف: " : "Grade: ") + grade!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(section))
            {
                parts.Add((arabic ? "الشعبة: " : "Section: ") + section!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(gender))
            {
                parts.Add((arabic ? "الجنس: " : "Gender: ") + gender!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                parts.Add((arabic ? "الحالة: " : "Status: ") + status!.Trim());
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                parts.Add((arabic ? "بحث: " : "Search: ") + query!.Trim());
            }

            return parts;
        }

        /// <summary>
        /// The download's filename. Dated so two exports taken a week apart do not sit in the
        /// same folder under one name, and ASCII so a browser that mangles a non-Latin
        /// <c>Content-Disposition</c> still hands over something openable.
        /// </summary>
        public static string FileName(DateTime takenAtUtc)
            => "students-" + takenAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv";
    }
}
