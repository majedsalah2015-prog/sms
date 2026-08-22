using System;
using System.Collections.Generic;
using Sms.Domain.Transport;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-TRN-003 / doc §9: stop times must be sequential along the route.</summary>
    public class StopTimesNotSequentialException : InvalidOperationException
    {
        public StopTimesNotSequentialException()
            : base("Route stop times must be strictly sequential (BR-TRN-003).")
        {
        }
    }

    /// <summary>BR-TRN-004 / doc §9: subscription dates within the academic year.</summary>
    public class SubscriptionDatesOutsideYearException : InvalidOperationException
    {
        public SubscriptionDatesOutsideYearException()
            : base("Transport subscription dates must fall within the academic year (BR-TRN-004).")
        {
        }
    }

    /// <summary>BR-TRN-004: one active subscription per enrollment.</summary>
    public class TransportSubscriptionExistsException : InvalidOperationException
    {
        public TransportSubscriptionExistsException(int studentId)
            : base($"Student {studentId} already has a live transport subscription this year (BR-TRN-004).")
        {
        }
    }

    /// <summary>BR-TRN-001: expired/missing mandatory bus documents block trip assignment.</summary>
    public class BusUnroadworthyException : InvalidOperationException
    {
        public BusUnroadworthyException(int busId, IReadOnlyCollection<BusDocumentKind> blockers)
            : base($"Bus {busId} is unroadworthy — {string.Join(", ", blockers)} missing or expired (BR-TRN-001).")
        {
            Blockers = blockers;
        }

        public IReadOnlyCollection<BusDocumentKind> Blockers { get; }
    }

    /// <summary>BR-TRN-002: driver licence missing, expired, or below the bus's required class.</summary>
    public class DriverNotEligibleException : InvalidOperationException
    {
        public DriverNotEligibleException(int driverId, int busId)
            : base($"Driver {driverId} is not licence-eligible for bus {busId} (BR-TRN-002).")
        {
        }
    }

    /// <summary>BR-TRN-005: a route has at most one trip per date.</summary>
    public class TripAlreadyOpenException : InvalidOperationException
    {
        public TripAlreadyOpenException(int routeId, DateTime date)
            : base($"Route {routeId} already has a trip on {date:yyyy-MM-dd} (BR-TRN-005).")
        {
        }
    }

    /// <summary>BR-TRN-005: only an InProgress trip accepts logs / closes.</summary>
    public class TripNotInProgressException : InvalidOperationException
    {
        public TripNotInProgressException(int tripId)
            : base($"Trip {tripId} is not in progress (BR-TRN-005).")
        {
        }
    }

    /// <summary>BR-TRN-005: the student is not on this trip's roster.</summary>
    public class StudentNotOnTripRosterException : InvalidOperationException
    {
        public StudentNotOnTripRosterException(int tripId, int studentId)
            : base($"Student {studentId} is not on trip {tripId}'s roster (BR-TRN-005).")
        {
        }
    }

    /// <summary>BR-TRN-005: unresolved students or no sweep confirmation.</summary>
    public class TripNotClosableException : InvalidOperationException
    {
        public TripNotClosableException(int tripId, IReadOnlyCollection<int> unresolvedStudentIds, bool sweepConfirmed)
            : base($"Trip {tripId} cannot close — unresolved students: [{string.Join(", ", unresolvedStudentIds)}], sweep confirmed: {sweepConfirmed} (BR-TRN-005).")
        {
            UnresolvedStudentIds = unresolvedStudentIds;
        }

        public IReadOnlyCollection<int> UnresolvedStudentIds { get; }
    }

    /// <summary>BR-TRN-006: PM handover to a person who is not pickup-authorized.</summary>
    public class HandoverNotAuthorizedException : InvalidOperationException
    {
        public HandoverNotAuthorizedException(int studentId, string? receivedByName)
            : base($"'{receivedByName}' is not authorized to receive student {studentId} (BR-TRN-006).")
        {
        }
    }

    /// <summary>BR-TRN-008 safety exception: never suspend mid-trip.</summary>
    public class SuspensionMidTripException : InvalidOperationException
    {
        public SuspensionMidTripException(int studentId)
            : base($"Student {studentId} is on a trip in progress — suspension must wait (BR-TRN-008).")
        {
        }
    }

    /// <summary>
    /// BR-TRN-003: a route may not be moved onto a bus with fewer seats than it already carries.
    /// Refused rather than waitlisting the overflow — the riders are already subscribed and already
    /// charged, and turning some of them back into applicants is not a reassignment.
    /// </summary>
    public class RouteCapacityExceededException : InvalidOperationException
    {
        public RouteCapacityExceededException(int routeId, int riders, int capacity)
            : base($"Route {routeId} carries {riders} rider(s); the chosen bus seats {capacity} (BR-TRN-003).")
        {
        }
    }
}
