namespace Sms.Domain.Students
{
    /// <summary>
    /// الوضع المادي — the family's means, as the school assesses them.
    /// <para>
    /// A judgement, not a measurement, and recorded as one: it drives who is
    /// offered a discount or a scholarship (Module 22), so it is deliberately a
    /// short ordered scale an administrator can defend rather than an income
    /// figure the school cannot verify.
    /// </para>
    /// </summary>
    public enum FinancialStatus : short
    {
        /// <summary>عادي</summary>
        Normal = 1,

        /// <summary>متوسط</summary>
        Medium = 2,

        /// <summary>جيد</summary>
        Good = 3,

        /// <summary>ممتاز</summary>
        Excellent = 4,
    }

    /// <summary>
    /// حالة الوالد — used for both parents.
    /// <para>
    /// <see cref="Martyr"/> and <see cref="Missing"/> are separate values, not
    /// shades of <see cref="Deceased"/>: in this region they carry distinct
    /// entitlements — fee exemptions, ministry reporting, benevolent-fund
    /// eligibility — and collapsing them would make those undiscoverable from
    /// the record. <see cref="Other"/> covers divorce, estrangement and
    /// emigration, which the school notes without classifying.
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

        /// <summary>مفقود</summary>
        Missing = 4,

        /// <summary>غير ذلك</summary>
        Other = 5,
    }

    /// <summary>الديانة. Kept coarse on purpose — the school records what it needs for religious-education streaming and nothing finer.</summary>
    public enum Religion : short
    {
        /// <summary>مسلم</summary>
        Muslim = 1,

        /// <summary>مسيحي</summary>
        Christian = 2,

        /// <summary>غير ذلك</summary>
        Other = 3,
    }

    /// <summary>
    /// مواطن / لاجئ — residency standing, which decides fee schedules,
    /// ration-card handling and ministry returns, so it is a first-class field
    /// rather than a note.
    /// </summary>
    public enum ResidencyStatus : short
    {
        /// <summary>مواطن</summary>
        Citizen = 1,

        /// <summary>لاجئ</summary>
        Refugee = 2,
    }
}
