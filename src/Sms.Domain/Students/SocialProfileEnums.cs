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

    // حالة الوالد moved to Sms.Domain.Parents.ParentLifeStatus when the parent
    // register grew a status of its own: two module namespaces cannot both declare
    // the name without CS0104 in the first file that imports both, and the concept
    // belongs to the parent rather than to the student's profile of them. On
    // 2026-08-24 the student's copy of it went as well (owner request) — a status is
    // recorded once, on the parent's file, and read from the guardians tab.

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
