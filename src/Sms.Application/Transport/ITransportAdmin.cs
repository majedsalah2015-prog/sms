using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Transport;

namespace Sms.Application.Transport
{
    public sealed record RouteStopInput(string NameAr, string NameEn, TimeSpan ScheduledTime, int ZoneFeeCategoryId, double? Latitude = null, double? Longitude = null);

    /// <summary>
    /// doc/Modules/23 §8 Fleet & documents / Route designer / Subscription
    /// desk / Trip console / Safety events screens backing (screens
    /// deferred, operations are core). Boarding notifications go through
    /// E-007's publisher (TransportStudentNotBoarded is the urgent class —
    /// quiet-hours bypass is the dispatcher's concern, BR-NOT-004).
    /// </summary>
    public interface ITransportAdmin
    {
        Task<Bus> RegisterBusAsync(string plateNo, int capacity, BusType type, LicenseClass requiredLicenseClass, CancellationToken cancellationToken = default);

        Task RecordBusDocumentAsync(int busId, BusDocumentKind kind, DateTime expiryDate, int? attachmentId = null, CancellationToken cancellationToken = default);

        Task<TransportStaff> RegisterStaffAsync(
            TransportStaffKind kind, string displayName, int? employeeId = null, string? contractorName = null,
            string? licenseNo = null, LicenseClass? licenseClass = null, DateTime? licenseExpiryDate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrects a driver's or attendant's own details — above all a renewed licence, which is a
        /// new expiry on the same person rather than a second person. Kind and the employee/contractor
        /// link do not change: which of the two a record is, and who it is, are what everything else
        /// points at.
        /// </summary>
        Task UpdateStaffAsync(
            int staffId, string displayName, string? licenseNo = null, LicenseClass? licenseClass = null,
            DateTime? licenseExpiryDate = null, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-003: stop times must be sequential (<see cref="Common.Exceptions.StopTimesNotSequentialException"/>); route number from doc 08 "RTE".</summary>
        Task<Route> DefineRouteAsync(
            string nameAr, string nameEn, RouteDirection direction, int busId, int driverId, IReadOnlyList<RouteStopInput> stops,
            int? attendantId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reassigns a route's standing bus and crew — a bus off the road for a week, a driver who
        /// left. Distinct from a per-trip substitution (BR-TRN-002), which changes one morning and
        /// leaves the route alone.
        /// <para>
        /// The new bus's capacity must still hold the route's current riders, and the new driver must
        /// be licensed for it — the same two checks trip-opening makes, applied here so the refusal
        /// arrives while somebody is choosing rather than at 07:00 tomorrow. Throws
        /// <see cref="Common.Exceptions.DriverNotEligibleException"/> or
        /// <see cref="Common.Exceptions.RouteCapacityExceededException"/>.
        /// </para>
        /// </summary>
        Task ReassignRouteCrewAsync(
            int routeId, int busId, int driverId, int? attendantId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-TRN-004: subscribes the student's working-year enrollment; posts the zone-priced transport charge for the AM (else PM)
        /// stop's fee category via E-303 PostChargeAsync. Capacity overflow on either route → Waitlisted + RouteWaitlist row instead
        /// of a charge (BR-TRN-003). Dates must fall inside the year (<see cref="Common.Exceptions.SubscriptionDatesOutsideYearException"/>).
        /// </summary>
        Task<TransportSubscription> SubscribeAsync(
            int studentId, int payerId, int? amRouteStopId, int? pmRouteStopId, DateTime startDate, DateTime? endDate = null,
            bool isSelfReleaseAllowed = false, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-004: family choice / withdrawal — ends the ride; charge policy (credit) is Module 19's, not applied here.</summary>
        Task EndSubscriptionAsync(int subscriptionId, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-007: mid-year stop reassignment — re-prices when the zone category changes (credit note on the old charge, new charge for the new zone) and notifies the family (TransportRouteChanged).</summary>
        Task ReassignStopsAsync(int subscriptionId, int? amRouteStopId, int? pmRouteStopId, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-008: Principal-approved, effective-dated arrears suspension; never mid-trip (<see cref="Common.Exceptions.SuspensionMidTripException"/>); notified (TransportSuspended).</summary>
        Task SuspendForArrearsAsync(int subscriptionId, DateTime effectiveDate, int approvedByUserId, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-TRN-005/001/002: opens the route's trip for a date — bus must be roadworthy (else <see cref="Common.Exceptions.BusUnroadworthyException"/>
        /// unless <paramref name="unroadworthyOverrideReason"/> — Principal, T1, logged as a SafetyEvent) and the driver licence-valid for the bus
        /// (<see cref="Common.Exceptions.DriverNotEligibleException"/>). Roster = active subscriptions on the route's stops.
        /// </summary>
        Task<Trip> OpenTripAsync(int routeId, DateTime date, int? substituteDriverId = null, int? substituteAttendantId = null, string? unroadworthyOverrideReason = null, CancellationToken cancellationToken = default);

        Task LogBoardingAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-TRN-006: on a PM trip where <paramref name="handoverRequired"/> (stage policy), the receiver must be a pickup-authorized guardian/emergency
        /// contact of the student unless the subscription allows self-release — otherwise <see cref="Common.Exceptions.HandoverNotAuthorizedException"/> and a T1 SafetyEvent.
        /// </summary>
        Task LogAlightingAsync(int tripId, int studentId, int actorUserId, bool handoverRequired = false, string? receivedByName = null, CancellationToken cancellationToken = default);

        Task DeclareAbsentAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-005: AM student not boarded → SafetyEvent + immediate parent notification (urgent class).</summary>
        Task RecordNotBoardedAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-005: PM student not collected at school → SafetyEvent escalated to supervisor.</summary>
        Task RecordNotCollectedAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-005: requires every roster student resolved and the sweep confirmed (<see cref="Common.Exceptions.TripNotClosableException"/>).</summary>
        Task CloseTripAsync(int tripId, bool sweepConfirmed, CancellationToken cancellationToken = default);

        /// <summary>BR-TRN-005: trips still InProgress after their date → Escalated + SafetyEvent (BR-ATD-007 pattern). Returns count escalated.</summary>
        Task<int> EscalateUnclosedTripsAsync(CancellationToken cancellationToken = default);

        Task ResolveSafetyEventAsync(int safetyEventId, string resolution, CancellationToken cancellationToken = default);
    }
}
