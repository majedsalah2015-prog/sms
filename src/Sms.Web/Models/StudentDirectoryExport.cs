using System;
using System.Collections.Generic;
using System.Globalization;

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
        /// One CSV cell, one CSV record and the finished bytes. The rules themselves live in
        /// <see cref="CsvFile"/>, where the parent file's export reads them too — the day the
        /// quoting has to change, it must change for every file this product hands out.
        /// </summary>
        public static string Cell(string? value) => CsvFile.Cell(value);

        /// <inheritdoc cref="CsvFile.Line"/>
        public static string Line(IEnumerable<string?> cells) => CsvFile.Line(cells);

        /// <inheritdoc cref="CsvFile.Bytes"/>
        public static byte[] Bytes(IEnumerable<IEnumerable<string?>> records) => CsvFile.Bytes(records);

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
