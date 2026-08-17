using System;
using Sms.Domain.Examinations;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-EXM-002: the exam's BlueprintComponent must belong to a Blueprint for the same offering + the round's term.</summary>
    public class ExamBlueprintMismatchException : InvalidOperationException
    {
        public ExamBlueprintMismatchException(int blueprintComponentId)
            : base($"Blueprint component {blueprintComponentId} does not match the exam's offering/term (BR-EXM-002).")
        {
        }
    }

    /// <summary>BR-EXM-003: more than the configured max exams already scheduled for this grade-year on this date.</summary>
    public class ExamScheduleClashException : InvalidOperationException
    {
        public ExamScheduleClashException(int gradeYearProfileId, DateTime date)
            : base($"Grade-year profile {gradeYearProfileId} already has the maximum exams scheduled on {date:yyyy-MM-dd} (BR-EXM-003).")
        {
        }
    }

    /// <summary>BR-EXM §4: the requested round status pair isn't a legal move.</summary>
    public class InvalidExamRoundStatusTransitionException : InvalidOperationException
    {
        public InvalidExamRoundStatusTransitionException(ExamRoundStatus from, ExamRoundStatus to)
            : base($"Exam round status cannot move from '{from}' to '{to}' (BR-EXM §4).")
        {
        }
    }

    /// <summary>BR-EXM-004/BR-ROM-002: the sitting's room is already at exam capacity.</summary>
    public class SittingFullException : InvalidOperationException
    {
        public SittingFullException(int examSittingId)
            : base($"Exam sitting {examSittingId} is at room exam capacity (BR-EXM-004/BR-ROM-002).")
        {
        }
    }

    /// <summary>The student was never seated in this sitting.</summary>
    public class StudentNotSeatedException : InvalidOperationException
    {
        public StudentNotSeatedException(int examSittingId, int enrollmentId)
            : base($"Enrollment {enrollmentId} is not seated in exam sitting {examSittingId}.")
        {
        }
    }
}
