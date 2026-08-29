using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Lookups;
using Sms.Domain.Lookups;

namespace Sms.Web.Models
{
    /// <summary>
    /// One catalogue on the staff reference screen: the lookup category behind it and what the tab
    /// calls it.
    /// <para>
    /// The tab title is plural and the category's own name is singular ("الجامعات" over a category
    /// named "الجامعة") because they answer different questions — the tab names a list, the
    /// category names what one of its rows is — and the screen shows both.
    /// </para>
    /// </summary>
    /// <param name="CategoryCode">core.LookupCategory.Code. The identity; never shown as a title.</param>
    /// <param name="CategoryNameAr">
    /// The singular name the category is created with, on the one path that creates one — a
    /// deployment whose seeder predates the list. An existing category keeps the name it has:
    /// the screen must not rename what the product or the school already settled on.
    /// </param>
    /// <param name="PlaceholderAr">A real example, so the first row somebody types has a shape to copy.</param>
    public sealed record StaffReferenceList(
        string CategoryCode,
        string TitleEn,
        string TitleAr,
        string CategoryNameEn,
        string CategoryNameAr,
        string Icon,
        string NoteEn,
        string NoteAr,
        string PlaceholderAr,
        string PlaceholderEn);

    /// <summary>
    /// الثوابت — the four lists the staff file picks from instead of typing (owner request,
    /// 2026-08-27).
    /// <para>
    /// The point of the screen is that these are catalogues rather than text boxes: two registrars
    /// typing "جامعة النجاح" and "جامعة النجاح الوطنية" produce two universities, and every count,
    /// filter and export built on the column afterwards is quietly wrong. A catalogue makes the
    /// second registrar pick the first one's row.
    /// </para>
    /// <para>
    /// All four are ordinary <c>core.LookupValue</c> lists rather than tables of their own. That is
    /// what the lookup framework is for (BR-SET-001), and it is what makes the rest work for free:
    /// tenant scoping, the T3 audit trail, the deactivate-never-delete guard (BR-SET-002,
    /// BR-GLB-005), the usage counter, and the Excel import's text matcher.
    /// </para>
    /// </summary>
    public static class StaffReferenceCatalogue
    {
        /// <summary>
        /// The order is the order of the qualification form: which degree, from where, in what, and
        /// separately the bank — so the screen reads in the same sequence as the tab that consumes it.
        /// </summary>
        public static IReadOnlyList<StaffReferenceList> All { get; } = new[]
        {
            // Deliberately the same category the parent record's qualification is chosen from, so
            // "بكالوريوس" is one value across the product rather than one per module. Renaming a row
            // here renames it on the parent file too — which is the point, and is why the note says so.
            new StaffReferenceList(
                "EducationLevel", "Qualifications", "المؤهلات", "Education Level", "المؤهل العلمي", "bi-award",
                "The degree or certificate itself. Shared with the parent record — a change here shows on both.",
                "المؤهل أو الشهادة نفسها. مشتركة مع سجل ولي الأمر — والتعديل هنا يظهر في الاثنين.",
                "بكالوريوس", "Bachelor"),

            new StaffReferenceList(
                "University", "Universities", "الجامعات", "University", "الجامعة", "bi-mortarboard",
                "The awarding bodies this school recognises. Ships empty on purpose: the list is a local one.",
                "الجهات المانحة التي تعتمدها المدرسة. تُسلَّم فارغة عن قصد: القائمة محلية تخص كل مدرسة.",
                "جامعة النجاح الوطنية", "An-Najah National University"),

            new StaffReferenceList(
                "Specialization", "Specializations", "التخصصات", "Specialization", "التخصص", "bi-bookmarks",
                "What the qualification is in. Also ships empty — a school's specializations follow its own subjects.",
                "مجال المؤهل. تُسلَّم فارغة أيضاً — تخصصات كل مدرسة تتبع موادها.",
                "رياضيات", "Mathematics"),

            // Not consumed by the employee file yet — see EmployeesController.Reference's remarks.
            // The note says so rather than letting a registrar fill in a list that goes nowhere.
            new StaffReferenceList(
                "Bank", "Banks", "البنوك", "Bank", "البنك", "bi-bank",
                "The banks salaries are paid into. The employee file still records the bank as free text — this list is not yet offered there.",
                "البنوك التي تُصرف فيها الرواتب. ملف الموظف ما زال يسجل اسم البنك نصاً حراً — وهذه القائمة لا تُعرض فيه بعد.",
                "بنك فلسطين", "Bank of Palestine"),
        };

        /// <summary>The list the screen opens on when the query string names none.</summary>
        public static StaffReferenceList Default => All[0];

        /// <summary>The named list, or null — an unknown code is a bad link, not a new catalogue.</summary>
        public static StaffReferenceList? Find(string? categoryCode)
            => string.IsNullOrWhiteSpace(categoryCode)
                ? null
                : All.FirstOrDefault(l => string.Equals(l.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>P-CFG over the four staff catalogues — one list open, the rest one click away.</summary>
    public sealed class StaffReferenceViewModel
    {
        public IReadOnlyList<StaffReferenceList> Lists { get; set; } = StaffReferenceCatalogue.All;

        public StaffReferenceList Selected { get; set; } = StaffReferenceCatalogue.Default;

        /// <summary>Active and inactive both — the screen reactivates, so it has to show what it can reactivate.</summary>
        public IReadOnlyList<LookupValue> Values { get; set; } = Array.Empty<LookupValue>();

        /// <summary>
        /// doc/Modules/01 §9: what already points at each value, keyed by value id, so the
        /// confirmation says "42 employees" before the click rather than after it. A value nothing
        /// references is absent rather than present-and-empty (see <see cref="ILookupUsageQuery"/>).
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<LookupUsage>> Usage { get; set; }
            = new Dictionary<int, IReadOnlyList<LookupUsage>>();

        /// <summary>Active values per category code — the counts on the rail, so an empty list is visible before it is opened.</summary>
        public IReadOnlyDictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();

        /// <summary>What the add form pre-fills, so rows land in the order they were typed.</summary>
        public int NextSortOrder { get; set; }
    }
}
