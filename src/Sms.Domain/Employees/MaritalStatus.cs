namespace Sms.Domain.Employees
{
    /// <summary>
    /// الحالة الاجتماعية للموظف.
    /// <para>
    /// Not in the entity list of doc/Modules/12 §7 — added at the owner's request (2026-08-23) so
    /// a school importing its existing staff register does not have to leave a column behind. It
    /// carries no rule of its own here: nothing in Module 12 branches on it, and it is not a
    /// discount input the way the student's family circumstances are (Module 22). It is recorded
    /// because a school's HR file has always recorded it.
    /// </para>
    /// <para>
    /// Kept coarse for the same reason <see cref="Students.Religion"/> is: a longer list invites
    /// entries the school cannot verify and no report will ever read.
    /// </para>
    /// </summary>
    public enum MaritalStatus : short
    {
        /// <summary>أعزب / عزباء</summary>
        Single = 1,

        /// <summary>متزوج / متزوجة</summary>
        Married = 2,

        /// <summary>مطلق / مطلقة</summary>
        Divorced = 3,

        /// <summary>أرمل / أرملة</summary>
        Widowed = 4,

        /// <summary>
        /// غير ذلك — added at the owner's request (2026-08-27) for the cases the four above do not
        /// name. Deliberately without a free-text note beside it: the four categories are what a
        /// staff register and a ministry return read, and a note nothing reports on would collect
        /// personal circumstances the school has no use for.
        /// </summary>
        Other = 5,
    }
}
