using Sms.Domain.Timetable;

namespace Sms.Application.Timetable
{
    /// <summary>Pure BR-TTB-002 WF-12 spine — the "zero hard-constraint violations to Validate/Publish" gate is enforced by the admin service, not this table.</summary>
    public static class TimetableVersionStatusTransitions
    {
        public static bool CanTransition(TimetableVersionStatus from, TimetableVersionStatus to)
        {
            return (from, to) switch
            {
                (TimetableVersionStatus.Draft, TimetableVersionStatus.Validated) => true,
                (TimetableVersionStatus.Validated, TimetableVersionStatus.Published) => true,
                (TimetableVersionStatus.Validated, TimetableVersionStatus.Draft) => true,
                _ => false,
            };
        }
    }
}
