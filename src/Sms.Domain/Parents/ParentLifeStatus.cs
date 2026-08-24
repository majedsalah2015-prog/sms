namespace Sms.Domain.Parents
{
    /// <summary>
    /// Whether a parent is alive, and if not, in which of the ways this system's
    /// schools actually have to record.
    /// <para>
    /// It is an enum rather than a lookup because it is not a list a school
    /// curates: other code reads these values and behaves differently for them. A
    /// guardian who is <see cref="Martyr"/> or <see cref="Deceased"/> should not be
    /// the family's notification contact, and both are grounds a hardship discount
    /// is granted on — a school-editable list could not be relied on for either.
    /// </para>
    /// <para>
    /// <see cref="Martyr"/> and <see cref="Missing"/> are separate values, not
    /// shades of <see cref="Deceased"/>: in this region they carry distinct
    /// entitlements — fee exemptions, ministry reporting, benevolent-fund
    /// eligibility — and collapsing them would make those undiscoverable from the
    /// record. <see cref="Other"/> covers divorce, estrangement and emigration,
    /// which the school notes without classifying.
    /// </para>
    /// <para>
    /// <b>It lives here, in Parents, and Students uses it.</b> It was declared in
    /// <c>Sms.Domain.Students.SocialProfileEnums</c> for the student social profile's
    /// father/mother pair, which meant any file importing both module namespaces got
    /// CS0104 the moment the parent register grew a status of its own — the exact
    /// collision this codebase has already renamed three entities to avoid. One
    /// definition in the module that owns the concept is the fix; the numbers are
    /// unchanged because <c>Student.FatherStatus</c> and <c>MotherStatus</c> are
    /// already persisted against them.
    /// </para>
    /// <para>
    /// SMALLINT-mapped and starting at 1 (DB/01 §5).
    /// </para>
    /// </summary>
    public enum ParentLifeStatus : short
    {
        /// <summary>على قيد الحياة</summary>
        Alive = 1,

        /// <summary>متوفى</summary>
        Deceased = 2,

        /// <summary>شهيد</summary>
        Martyr = 3,

        /// <summary>مفقود — whereabouts unknown; deliberately distinct from deceased, because the family's legal and financial position is not the same.</summary>
        Missing = 4,

        /// <summary>غير ذلك — recorded with a note, so a school is never forced to file a person under a category that does not fit.</summary>
        Other = 5,
    }
}
