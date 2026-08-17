using System;
using Sms.Domain.Activities;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>doc/Modules/29 §4: the requested program status pair isn't a legal move.</summary>
    public class InvalidProgramStatusTransitionException : InvalidOperationException
    {
        public InvalidProgramStatusTransitionException(ProgramStatus from, ProgramStatus to)
            : base($"Program status cannot move from '{from}' to '{to}' (doc/Modules/29 §4).")
        {
        }
    }

    /// <summary>BR-ACT-002/005: the requested enrollment status pair isn't a legal move.</summary>
    public class InvalidProgramEnrollmentStatusTransitionException : InvalidOperationException
    {
        public InvalidProgramEnrollmentStatusTransitionException(ProgramEnrollmentStatus from, ProgramEnrollmentStatus to)
            : base($"Program enrollment status cannot move from '{from}' to '{to}' (BR-ACT-002/005).")
        {
        }
    }

    /// <summary>BR-ACT-005: no consent on file for a program that requires it — hard, no override.</summary>
    public class ConsentRequiredException : InvalidOperationException
    {
        public ConsentRequiredException(int programEnrollmentId)
            : base($"Program enrollment {programEnrollmentId} requires consent before activation (BR-ACT-005).")
        {
        }
    }

    /// <summary>BR-ACT-004: the trip's staff ratio isn't satisfied, all active enrollments don't have current consent, or the transport plan isn't confirmed.</summary>
    public class TripNotReadyForDepartureException : InvalidOperationException
    {
        public TripNotReadyForDepartureException(int activityTripId)
            : base($"Activity trip {activityTripId} is not ready for departure (BR-ACT-004).")
        {
        }
    }

    /// <summary>BR-TRN-005 sweep pattern: the returned headcount doesn't match who departed.</summary>
    public class TripHeadcountMismatchException : InvalidOperationException
    {
        public TripHeadcountMismatchException(int departedCount, int returnedCount)
            : base($"Trip headcount mismatch: {departedCount} departed, {returnedCount} confirmed on return (BR-ACT-004/BR-TRN-005).")
        {
        }
    }
}
