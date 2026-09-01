namespace Sms.Domain.Learning
{
    /// <summary>
    /// doc/Modules/37 §4 content lifecycle. Starts at 1 per the SMALLINT
    /// convention (docs/Database/01) — module 37 gives no explicit numbering.
    /// There is no Published -> Draft edge: BR-LRN-003 makes publication the
    /// event families see, so withdrawing content from the portal is a
    /// <see cref="Retired"/> transition (BR-LRN-016), never an un-publish that
    /// would make a lesson silently vanish from a student's week.
    /// </summary>
    public enum LessonStatus
    {
        Draft = 1,
        Published = 2,
        Retired = 3,
    }
}
