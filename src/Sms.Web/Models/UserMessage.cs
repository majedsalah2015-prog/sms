using System;
using Sms.Application.Common.Exceptions;

namespace Sms.Web.Models
{
    /// <summary>
    /// Turns an engine exception into the sentence a user should read, in their language.
    /// <para>
    /// The layering is deliberate. Domain and Application exceptions carry English messages because
    /// that is what a log entry should say, identically in every deployment. But those messages were
    /// reaching Arabic-speaking administrators unchanged, and an English-only refusal is a dead end:
    /// the reader cannot tell what was rejected, let alone what would have been accepted.
    /// </para>
    /// <para>
    /// So translation happens here, at the boundary, keyed on the exception's type and its typed
    /// properties rather than on its text. Anything not listed falls through to the original message
    /// — a wrong-language sentence is bad, an empty one is worse.
    /// </para>
    /// </summary>
    public static class UserMessage
    {
        public static string For(Exception exception, bool arabic) => exception switch
        {
            MissingAuditReasonException e => arabic
                ? $"تغيير «{FieldName(e.EntityType, e.FieldName, true)}» يتطلب كتابة سبب — الحقل من الفئة الأولى في التدقيق."
                : $"Changing \"{FieldName(e.EntityType, e.FieldName, false)}\" requires a reason — the field is audited at tier 1.",

            InvalidSettingValueException e => arabic
                ? $"لم تُقبَل هذه القيمة لـ «{SettingLabels.Name(e.Key, true)}»."
                : $"\"{SettingLabels.Name(e.Key, false)}\" would not accept that value.",

            _ => exception.Message,
        };

        /// <summary>
        /// The field's name as the screen calls it. Only the fields a user actually meets are
        /// listed; anything else falls back to the entity and field as the model names them, which
        /// is still more use than nothing when a new T1 field appears before this table is updated.
        /// </summary>
        private static string FieldName(string entityType, string field, bool arabic) => (entityType, field) switch
        {
            ("SchoolSetting", "Value") => arabic ? "قيمة الإعداد" : "the setting's value",
            ("Student", "FirstNameAr") or ("Student", "FirstNameEn") => arabic ? "اسم الطالب" : "the student's name",
            ("Student", "FamilyNameAr") or ("Student", "FamilyNameEn") => arabic ? "اسم عائلة الطالب" : "the student's family name",
            ("Student", "PrimaryIdNo") => arabic ? "رقم هوية الطالب" : "the student's ID number",
            ("Student", "DateOfBirth") => arabic ? "تاريخ ميلاد الطالب" : "the student's date of birth",
            ("Parent", "NameAr") or ("Parent", "NameEn") => arabic ? "اسم ولي الأمر" : "the parent's name",
            ("Employee", "FirstNameAr") or ("Employee", "FirstNameEn") => arabic ? "اسم الموظف" : "the employee's name",
            ("AttendanceDay", "Status") => arabic ? "حالة الحضور" : "the attendance status",
            ("School", "NameAr") or ("School", "NameEn") => arabic ? "اسم المدرسة" : "the school's name",
            _ => arabic ? $"{entityType}.{field}" : $"{entityType}.{field}",
        };
    }
}
