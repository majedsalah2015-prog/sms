using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure doc/Modules/37 §4 content spine: draft -> published -> retired.
    ///
    /// Deliberately missing edge: Published -> Draft. BR-LRN-003 makes
    /// publication the event families see and the event that raises
    /// notifications, so there is no un-publish — content leaves the portal by
    /// being retired (BR-LRN-016), which states a reason and keeps the row
    /// readable. An un-publish would let a lesson a student read on Sunday
    /// disappear on Monday with no trace and no explanation.
    ///
    /// Retired is terminal: re-teaching the material is a new lesson, not a
    /// resurrection of a withdrawn one.
    /// </summary>
    public static class LessonStatusTransitions
    {
        public static bool CanTransition(LessonStatus from, LessonStatus to)
        {
            return (from, to) switch
            {
                (LessonStatus.Draft, LessonStatus.Published) => true,
                (LessonStatus.Draft, LessonStatus.Retired) => true,
                (LessonStatus.Published, LessonStatus.Retired) => true,
                _ => false,
            };
        }

        /// <summary>BR-LRN-003: only published content is visible in the portal (BR-GLB-031, BR-SEC-012).</summary>
        public static bool IsVisibleToPortal(LessonStatus status) => status == LessonStatus.Published;
    }
}
