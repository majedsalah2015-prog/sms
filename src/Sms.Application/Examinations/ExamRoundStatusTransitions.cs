using Sms.Domain.Examinations;

namespace Sms.Application.Examinations
{
    /// <summary>Pure BR-EXM §4 spine — mirrors Timetable's WF-12 shape (Draft -> Validated -> Published).</summary>
    public static class ExamRoundStatusTransitions
    {
        public static bool CanTransition(ExamRoundStatus from, ExamRoundStatus to)
        {
            return (from, to) switch
            {
                (ExamRoundStatus.Draft, ExamRoundStatus.Validated) => true,
                (ExamRoundStatus.Validated, ExamRoundStatus.Published) => true,
                (ExamRoundStatus.Validated, ExamRoundStatus.Draft) => true,
                _ => false,
            };
        }
    }
}
