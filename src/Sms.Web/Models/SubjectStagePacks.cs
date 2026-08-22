using System;
using System.Collections.Generic;

namespace Sms.Web.Models
{
    /// <summary>
    /// The subject list a school of each stage starts from, so the catalog does not have to be typed
    /// one row at a time on the first day.
    /// <para>
    /// A starting point, not a curriculum: every row can be renamed, recategorised or deactivated
    /// afterwards, and loading a pack never touches a code that already exists — which is what makes
    /// pressing the same button twice harmless, and what lets a school load the preparatory pack on
    /// top of the primary one when it teaches both.
    /// </para>
    /// <para>
    /// Codes are the short forms the rest of the product shows in tables, so they are kept stable and
    /// shared across stages: <c>MATH</c> is the same subject in grade 2 and grade 11, and the plan
    /// editor is where the two differ in periods and weight.
    /// </para>
    /// </summary>
    public static class SubjectStagePacks
    {
        public sealed record Row(string Code, string NameAr, string NameEn, string Category);

        public const string Primary = "primary";

        public const string Preparatory = "preparatory";

        public const string Secondary = "secondary";

        public static readonly string[] All = { Primary, Preparatory, Secondary };

        public static string Label(string stage, bool arabic) => stage switch
        {
            Primary => arabic ? "ابتدائي" : "Primary",
            Preparatory => arabic ? "إعدادي" : "Preparatory",
            Secondary => arabic ? "ثانوي" : "Secondary",
            _ => stage,
        };

        /// <summary>Grades 1–4: one general science, one social subject, and the two activity subjects.</summary>
        private static readonly Row[] PrimaryPack =
        {
            new("ISLM", "التربية الإسلامية", "Islamic Education", "religious"),
            new("ARAB", "اللغة العربية", "Arabic Language", "language"),
            new("ENGL", "اللغة الإنجليزية", "English Language", "language"),
            new("MATH", "الرياضيات", "Mathematics", "core"),
            new("SCI", "العلوم والحياة", "Science and Life", "core"),
            new("SOCS", "التربية الاجتماعية والوطنية", "Social and National Education", "core"),
            new("TECH", "التكنولوجيا", "Technology", "core"),
            new("ART", "التربية الفنية", "Art Education", "activity"),
            new("PE", "التربية الرياضية", "Physical Education", "activity"),
        };

        /// <summary>Grades 5–9: the primary list, with the social subject splitting into history and geography.</summary>
        private static readonly Row[] PreparatoryPack =
        {
            new("ISLM", "التربية الإسلامية", "Islamic Education", "religious"),
            new("ARAB", "اللغة العربية", "Arabic Language", "language"),
            new("ENGL", "اللغة الإنجليزية", "English Language", "language"),
            new("MATH", "الرياضيات", "Mathematics", "core"),
            new("SCI", "العلوم والحياة", "Science and Life", "core"),
            new("HIST", "التاريخ", "History", "core"),
            new("GEOG", "الجغرافيا", "Geography", "core"),
            new("NATL", "التربية الوطنية والحياتية", "National and Life Education", "core"),
            new("TECH", "التكنولوجيا", "Technology", "core"),
            new("VOC", "التربية المهنية", "Vocational Education", "core"),
            new("ART", "التربية الفنية", "Art Education", "activity"),
            new("PE", "التربية الرياضية", "Physical Education", "activity"),
        };

        /// <summary>
        /// Grades 10–12: the shared subjects plus both streams' specialisms, since one catalog serves
        /// the whole school and the plan editor is what decides which grade-year is offered what.
        /// </summary>
        private static readonly Row[] SecondaryPack =
        {
            new("ISLM", "التربية الإسلامية", "Islamic Education", "religious"),
            new("ARAB", "اللغة العربية", "Arabic Language", "language"),
            new("ENGL", "اللغة الإنجليزية", "English Language", "language"),
            new("MATH", "الرياضيات", "Mathematics", "core"),
            new("PHYS", "الفيزياء", "Physics", "core"),
            new("CHEM", "الكيمياء", "Chemistry", "core"),
            new("BIOL", "الأحياء", "Biology", "core"),
            new("EART", "علوم الأرض والبيئة", "Earth and Environmental Science", "core"),
            new("SCIC", "الثقافة العلمية", "Scientific Culture", "core"),
            new("HIST", "التاريخ", "History", "core"),
            new("GEOG", "الجغرافيا", "Geography", "core"),
            new("TECH", "التكنولوجيا", "Technology", "core"),
            new("FREN", "اللغة الفرنسية", "French Language", "elective"),
            new("PE", "التربية الرياضية", "Physical Education", "activity"),
        };

        public static IReadOnlyList<Row> For(string? stage) => stage switch
        {
            Primary => PrimaryPack,
            Preparatory => PreparatoryPack,
            Secondary => SecondaryPack,
            _ => Array.Empty<Row>(),
        };
    }
}
