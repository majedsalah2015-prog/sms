using System.Collections.Generic;
using System.Linq;
using Sms.Application.Lookups;

namespace Sms.Web.Models
{
    /// <summary>
    /// Says, in the reader's language, what is pointing at a lookup value before
    /// they retire it (doc/Modules/01 §9, BR-SET-002).
    /// <para>
    /// The usage query returns CLR names — "Student", "NationalityLookupId" —
    /// deliberately, so the wording stays a Web-boundary decision. Keyed on the
    /// <em>pair</em>, because one entity references several lookups for different
    /// reasons: a student's nationality, their ID type and their mother's education
    /// are three different sentences and only one of them is "students".
    /// </para>
    /// <para>
    /// Two names read wrong if taken literally. <c>Application</c> is
    /// <c>Sms.Domain.Admissions.Application</c> — an admission application, nothing
    /// to do with software; it was renamed around a CS0118 collision and the CLR name
    /// kept the trap. <c>EmployeeAssignment</c> is a posting, not a person, so
    /// retiring a job title reads as "N postings", not "N employees".
    /// </para>
    /// </summary>
    public static class LookupUsageLabels
    {
        private static readonly Dictionary<string, (string En, string Ar)> Names = new()
        {
            ["Student/NationalityLookupId"] = ("students (nationality)", "طلاب (الجنسية)"),
            ["Student/PrimaryIdTypeLookupId"] = ("students (ID type)", "طلاب (نوع الهوية)"),
            ["Student/MotherEducationLookupId"] = ("students (mother's education)", "طلاب (تعليم الأم)"),
            ["StudentGuardianLink/RelationshipLookupId"] = ("guardian links (relationship)", "روابط أولياء الأمر (صلة القرابة)"),
            ["Employee/NationalityLookupId"] = ("employees (nationality)", "موظفون (الجنسية)"),
            ["Employee/PrimaryIdTypeLookupId"] = ("employees (ID type)", "موظفون (نوع الهوية)"),
            ["EmployeeAssignment/PositionLookupId"] = ("job postings (position)", "تعيينات (المسمى الوظيفي)"),
            ["Application/NationalityLookupId"] = ("admission applications (nationality)", "طلبات التحاق (الجنسية)"),
            ["EmergencyContact/RelationshipLookupId"] = ("emergency contacts (relationship)", "جهات اتصال للطوارئ (صلة القرابة)"),
            ["Room/RoomTypeLookupId"] = ("rooms (room type)", "قاعات (نوع القاعة)"),
            ["RoomFeature/FeatureLookupId"] = ("room features", "تجهيزات القاعات"),
        };

        /// <summary>
        /// The phrase for one usage row. An unmapped pair falls back to the CLR names
        /// rather than to nothing: a new referencing column will appear here as
        /// "Student · SomeNewLookupId" until someone writes the sentence, which is
        /// ugly but still tells the operator what is holding the value.
        /// </summary>
        public static string Describe(LookupUsage usage, bool arabic)
        {
            var key = $"{usage.EntityName}/{usage.PropertyName}";
            return Names.TryGetValue(key, out var name)
                ? (arabic ? name.Ar : name.En)
                : $"{usage.EntityName} · {usage.PropertyName}";
        }

        /// <summary>A one-line summary for a table cell: the total, then the two biggest holders.</summary>
        public static string Summarize(IReadOnlyList<LookupUsage> usages, bool arabic)
        {
            if (usages.Count == 0)
            {
                return arabic ? "لا شيء يشير إليها" : "nothing refers to it";
            }

            var total = usages.Sum(u => u.Count);
            var top = string.Join("، ", usages.Take(2).Select(u => $"{u.Count} {Describe(u, arabic)}"));
            return usages.Count <= 2
                ? top
                : (arabic ? $"{total} إجمالاً — {top}، وغيرها" : $"{total} in total — {top}, and more");
        }
    }
}
