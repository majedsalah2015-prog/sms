using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sms.Web.Models
{
    /// <summary>
    /// The parent file as a file somebody opens somewhere else (doc/Modules/11 §8.2, §10 "Family
    /// register — parents with children count, balances").
    /// <para>
    /// One row per child rather than one row per parent, with the parent's identity repeated down
    /// the left. A family is a table only when it is written that way: a single row carrying "3
    /// children" answers nothing a registrar reconciling a bus list or a ministry return actually
    /// asks, and several of these exports concatenate into one register precisely because the
    /// parent columns repeat.
    /// </para>
    /// <para>
    /// Separated from the controller because the shaping is exactly the sort of thing that is
    /// wrong in a way nobody notices — a row one cell short of its headings shifts every column
    /// after it, and the file still opens. Each rule below is pinned by a test rather than by a
    /// reading.
    /// </para>
    /// </summary>
    public static class ParentFileExport
    {
        /// <summary>The parent's own columns, repeated on every row of the family.</summary>
        private const int ParentColumns = 4;

        /// <summary>
        /// The column headings, in the reader's language. The file is read by the person who asked
        /// for it, not by a machine, so the headings follow the screen rather than staying English
        /// for the sake of a stable schema.
        /// </summary>
        public static IReadOnlyList<string> Headings(bool arabic) => new[]
        {
            arabic ? "رقم ملف ولي الأمر" : "Parent file no.",
            arabic ? "ولي الأمر" : "Parent",
            arabic ? "الجوال" : "Mobile",
            arabic ? "البريد" : "Email",
            arabic ? "رقم الطالب" : "Student no.",
            arabic ? "الطالب" : "Student",
            arabic ? "الصف" : "Grade",
            arabic ? "القرابة" : "Relationship",
            arabic ? "جهة اتصال أساسية" : "Primary contact",
            arabic ? "مسؤول مالياً" : "Financially responsible",
            arabic ? "مخوَّل بالاستلام" : "Pickup authorized",
            arabic ? "ظاهر في البوابة" : "Portal visible",
            arabic ? "مرتبط منذ" : "Linked since",
            arabic ? "انتهى الربط" : "Link ended",
            arabic ? "مرحّل" : "Posted",
            arabic ? "إشعارات دائنة" : "Credit notes",
            arabic ? "خصومات" : "Discounts",
            arabic ? "مدفوع" : "Paid",
            arabic ? "الرصيد" : "Balance",
        };

        /// <summary>
        /// The whole file: the headings, then the current links, then the ended ones.
        /// <para>
        /// Past links are included with their end date filled in, because a family export that
        /// silently drops the child who left in March is a quiet omission on a document the school
        /// keeps. Their money columns stay empty: the family statement consolidates the current
        /// links (BR-FEE-008), and a figure printed against an ended link would be read as owing.
        /// </para>
        /// <para>
        /// A parent with nobody linked still exports one row carrying the identity — the parent is
        /// what was asked for, and a file holding only headings reads as the export having failed
        /// rather than as the fact that it is.
        /// </para>
        /// </summary>
        public static IReadOnlyList<IEnumerable<string?>> Records(ParentFileViewModel file, bool arabic)
        {
            var p = file.Parent;
            var parent = new[]
            {
                p.ParentFileNo,
                arabic ? p.NameAr : p.NameEn,
                p.PrimaryMobile,
                p.Email ?? string.Empty,
            };

            var records = new List<IEnumerable<string?>> { Headings(arabic) };

            IEnumerable<string?> Row(ParentFileViewModel.ChildRow c)
            {
                var line = file.FamilyStatement.FirstOrDefault(l => l.Student.Id == c.Student.Id);
                return parent.Concat(new[]
                {
                    c.Student.StudentNo,
                    arabic
                        ? $"{c.Student.FirstNameAr} {c.Student.FatherNameAr} {c.Student.FamilyNameAr}"
                        : $"{c.Student.FirstNameEn} {c.Student.FatherNameEn} {c.Student.FamilyNameEn}",
                    c.GradeName ?? string.Empty,
                    c.Relationship,
                    Flag(c.Link.IsPrimaryContact, arabic),
                    Flag(c.Link.IsFinanciallyResponsible, arabic),
                    Flag(c.Link.IsPickupAuthorized, arabic),
                    Flag(c.Link.IsPortalVisible, arabic),
                    Date(c.Link.EffectiveFromUtc),
                    Date(c.Link.EffectiveToUtc),
                    line == null ? string.Empty : Money(line.Gross),
                    line == null ? string.Empty : Money(line.CreditNotes),
                    line == null ? string.Empty : Money(line.Discounts),
                    line == null ? string.Empty : Money(line.Paid),
                    line == null ? string.Empty : Money(line.Position),
                });
            }

            records.AddRange(file.Children.Select(Row));
            records.AddRange(file.PastChildren.Select(Row));

            if (file.Children.Count == 0 && file.PastChildren.Count == 0)
            {
                records.Add(parent.Concat(Enumerable.Repeat(string.Empty, Headings(arabic).Count - ParentColumns)));
            }

            return records;
        }

        /// <summary>Yes/no in the reader's language — the screen's ✓/— means nothing in a spreadsheet.</summary>
        public static string Flag(bool value, bool arabic) => arabic ? (value ? "نعم" : "لا") : (value ? "Yes" : "No");

        /// <summary>
        /// A money column. Invariant with two decimals and no thousands separator, because the
        /// destination is another system: the screen's <c>N2</c> renders 1,250.00, which a
        /// spreadsheet under an Arabic locale reads as text and a ministry import rejects.
        /// </summary>
        public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>A date column, ISO and invariant; empty when there is no date.</summary>
        public static string Date(DateTime? value)
            => value == null ? string.Empty : value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>
        /// The download's filename. Carries the parent's file number so two families' exports do
        /// not sit in a folder under one name, and is dated for the same reason. ASCII only — see
        /// <see cref="CsvFile.Slug"/>.
        /// </summary>
        public static string FileName(string? parentFileNo, DateTime takenAtUtc)
            => "parent-" + CsvFile.Slug(parentFileNo, "file")
                + "-" + takenAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv";
    }
}
