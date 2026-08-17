using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Application.Transport;
using Sms.Domain.Fees;
using Sms.Domain.Students;
using Sms.Domain.Transport;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Transport
{
    /// <summary>Standalone — saves itself. Roster membership and roadworthiness are derived on demand, never stored.</summary>
    public class TransportAdmin : ITransportAdmin
    {
        public const string RouteSeriesCode = "RTE";
        public const string NotBoardedEventCode = "TransportStudentNotBoarded";
        public const string RouteChangedEventCode = "TransportRouteChanged";
        public const string SuspendedEventCode = "TransportSuspended";

        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly IFeeAdmin _feeAdmin;
        private readonly INotificationPublisher _notifications;

        public TransportAdmin(
            AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit, IWorkingYearContext workingYear,
            IFeeAdmin feeAdmin, INotificationPublisher notifications)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
            _workingYear = workingYear;
            _feeAdmin = feeAdmin;
            _notifications = notifications;
        }

        // ------------------------------------------------------------------ fleet + staff

        public async Task<Bus> RegisterBusAsync(string plateNo, int capacity, BusType type, LicenseClass requiredLicenseClass, CancellationToken cancellationToken = default)
        {
            var bus = new Bus { PlateNo = plateNo, Capacity = capacity, Type = type, RequiredLicenseClass = requiredLicenseClass };
            _db.Buses.Add(bus);
            await _db.SaveChangesAsync(cancellationToken);
            return bus;
        }

        public async Task RecordBusDocumentAsync(int busId, BusDocumentKind kind, DateTime expiryDate, int? attachmentId = null, CancellationToken cancellationToken = default)
        {
            _db.BusDocuments.Add(new BusDocument { BusId = busId, Kind = kind, ExpiryDate = expiryDate.Date, AttachmentId = attachmentId });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<TransportStaff> RegisterStaffAsync(
            TransportStaffKind kind, string displayName, int? employeeId = null, string? contractorName = null,
            string? licenseNo = null, LicenseClass? licenseClass = null, DateTime? licenseExpiryDate = null, CancellationToken cancellationToken = default)
        {
            var staff = new TransportStaff
            {
                Kind = kind, DisplayName = displayName, EmployeeId = employeeId, ContractorName = contractorName,
                LicenseNo = licenseNo, LicenseClass = licenseClass, LicenseExpiryDate = licenseExpiryDate?.Date,
            };
            _db.TransportStaff.Add(staff);
            await _db.SaveChangesAsync(cancellationToken);
            return staff;
        }

        // ------------------------------------------------------------------ routes

        public async Task<Route> DefineRouteAsync(
            string nameAr, string nameEn, RouteDirection direction, int busId, int driverId, IReadOnlyList<RouteStopInput> stops,
            int? attendantId = null, CancellationToken cancellationToken = default)
        {
            if (!StopSequenceValidator.AreSequential(stops.Select(s => s.ScheduledTime).ToList()))
            {
                throw new StopTimesNotSequentialException();
            }

            var route = new Route
            {
                AcademicYearId = _workingYear.AcademicYearId, RouteNo = await _numberIssuer.IssueAsync(RouteSeriesCode, cancellationToken),
                NameAr = nameAr, NameEn = nameEn, Direction = direction, BusId = busId, DriverId = driverId, AttendantId = attendantId,
            };
            for (var i = 0; i < stops.Count; i++)
            {
                route.Stops.Add(new RouteStop
                {
                    SequenceNumber = i + 1, NameAr = stops[i].NameAr, NameEn = stops[i].NameEn, ScheduledTime = stops[i].ScheduledTime,
                    ZoneFeeCategoryId = stops[i].ZoneFeeCategoryId, Latitude = stops[i].Latitude, Longitude = stops[i].Longitude,
                });
            }

            _db.Routes.Add(route);
            await _db.SaveChangesAsync(cancellationToken);
            return route;
        }

        // ------------------------------------------------------------------ subscriptions

        private async Task<(Route Route, RouteStop Stop, int Capacity)> LoadStopContextAsync(int routeStopId, CancellationToken cancellationToken)
        {
            var stop = await _db.RouteStops.SingleAsync(s => s.Id == routeStopId, cancellationToken);
            var route = await _db.Routes.SingleAsync(r => r.Id == stop.RouteId, cancellationToken);
            var bus = await _db.Buses.SingleAsync(b => b.Id == route.BusId, cancellationToken);
            return (route, stop, bus.Capacity);
        }

        private async Task<int> CountActiveRidersAsync(Route route, CancellationToken cancellationToken)
        {
            var stopIds = await _db.RouteStops.Where(s => s.RouteId == route.Id).Select(s => s.Id).ToListAsync(cancellationToken);
            return await _db.TransportSubscriptions.CountAsync(
                s => s.Status == TransportSubscriptionStatus.Active
                     && ((route.Direction == RouteDirection.Am && s.AmRouteStopId != null && stopIds.Contains(s.AmRouteStopId.Value))
                         || (route.Direction == RouteDirection.Pm && s.PmRouteStopId != null && stopIds.Contains(s.PmRouteStopId.Value))),
                cancellationToken);
        }

        public async Task<TransportSubscription> SubscribeAsync(
            int studentId, int payerId, int? amRouteStopId, int? pmRouteStopId, DateTime startDate, DateTime? endDate = null,
            bool isSelfReleaseAllowed = false, CancellationToken cancellationToken = default)
        {
            var yearId = _workingYear.AcademicYearId;
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == yearId, cancellationToken);
            if (startDate.Date < year.StartDate.Date || startDate.Date > year.EndDate.Date || (endDate.HasValue && (endDate.Value.Date > year.EndDate.Date || endDate.Value.Date < startDate.Date)))
            {
                throw new SubscriptionDatesOutsideYearException();
            }

            var enrollment = await _db.Enrollments.SingleAsync(e => e.StudentId == studentId && e.AcademicYearId == yearId, cancellationToken);
            var live = await _db.TransportSubscriptions.AnyAsync(
                s => s.EnrollmentId == enrollment.Id && s.Status != TransportSubscriptionStatus.Ended, cancellationToken);
            if (live)
            {
                throw new TransportSubscriptionExistsException(studentId);
            }

            var waitlistRoutes = new List<int>();
            int? zoneCategoryId = null;
            foreach (var stopId in new[] { amRouteStopId, pmRouteStopId }.Where(id => id.HasValue).Select(id => id!.Value))
            {
                var (route, stop, capacity) = await LoadStopContextAsync(stopId, cancellationToken);
                zoneCategoryId ??= stop.ZoneFeeCategoryId;
                if (!RouteCapacityEvaluator.HasSeat(await CountActiveRidersAsync(route, cancellationToken), capacity))
                {
                    waitlistRoutes.Add(route.Id);
                }
            }

            var subscription = new TransportSubscription
            {
                AcademicYearId = yearId, EnrollmentId = enrollment.Id, StudentId = studentId, PayerId = payerId,
                AmRouteStopId = amRouteStopId, PmRouteStopId = pmRouteStopId, StartDate = startDate.Date, EndDate = endDate?.Date,
                IsSelfReleaseAllowed = isSelfReleaseAllowed,
                Status = waitlistRoutes.Count > 0 ? TransportSubscriptionStatus.Waitlisted : TransportSubscriptionStatus.Active,
            };
            _db.TransportSubscriptions.Add(subscription);
            await _db.SaveChangesAsync(cancellationToken);

            if (waitlistRoutes.Count > 0)
            {
                foreach (var routeId in waitlistRoutes.Distinct())
                {
                    _db.RouteWaitlists.Add(new RouteWaitlist { RouteId = routeId, TransportSubscriptionId = subscription.Id, QueuedAtUtc = _clock.UtcNow });
                }

                await _db.SaveChangesAsync(cancellationToken);
                return subscription;
            }

            // BR-TRN-004: registration triggers the zone-priced transport charge (BR-FEE-003 service-linked). Pro-ration (BR-FEE-006)
            // is E-303's deferral, so a mid-year start still posts the structure amount - flagged, not faked.
            if (zoneCategoryId.HasValue)
            {
                var charge = await _feeAdmin.PostChargeAsync(studentId, payerId, enrollment.GradeYearProfileId, zoneCategoryId.Value, ChargeSourceType.ServiceAssignment, cancellationToken);
                subscription.ChargeId = charge.Id;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return subscription;
        }

        public async Task EndSubscriptionAsync(int subscriptionId, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var subscription = await _db.TransportSubscriptions.SingleAsync(s => s.Id == subscriptionId, cancellationToken);
            subscription.EndDate = endDate.Date;
            subscription.Status = TransportSubscriptionStatus.Ended;
            var waitlist = await _db.RouteWaitlists.Where(w => w.TransportSubscriptionId == subscriptionId && w.IsActive).ToListAsync(cancellationToken);
            waitlist.ForEach(w => w.IsActive = false);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReassignStopsAsync(int subscriptionId, int? amRouteStopId, int? pmRouteStopId, CancellationToken cancellationToken = default)
        {
            var subscription = await _db.TransportSubscriptions.SingleAsync(s => s.Id == subscriptionId, cancellationToken);
            var oldPrimary = subscription.AmRouteStopId ?? subscription.PmRouteStopId;
            var newPrimary = amRouteStopId ?? pmRouteStopId;
            var oldZone = oldPrimary.HasValue ? (await _db.RouteStops.SingleAsync(s => s.Id == oldPrimary.Value, cancellationToken)).ZoneFeeCategoryId : (int?)null;
            var newZone = newPrimary.HasValue ? (await _db.RouteStops.SingleAsync(s => s.Id == newPrimary.Value, cancellationToken)).ZoneFeeCategoryId : (int?)null;

            subscription.AmRouteStopId = amRouteStopId;
            subscription.PmRouteStopId = pmRouteStopId;
            await _db.SaveChangesAsync(cancellationToken);

            // BR-TRN-007: re-price on zone change - credit the old zone charge's uncredited balance, post the new zone's charge.
            if (newZone.HasValue && oldZone != newZone && subscription.ChargeId.HasValue)
            {
                var old = await _db.Charges.SingleAsync(c => c.Id == subscription.ChargeId.Value, cancellationToken);
                var credited = (await _db.CreditNotes.Where(n => n.ChargeId == old.Id).Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
                if (old.GrossAmount - credited > 0m)
                {
                    await _feeAdmin.IssueCreditNoteAsync(old.Id, old.GrossAmount - credited, "transport zone reassignment (BR-TRN-007)", cancellationToken);
                }

                var enrollment = await _db.Enrollments.SingleAsync(e => e.Id == subscription.EnrollmentId, cancellationToken);
                var charge = await _feeAdmin.PostChargeAsync(subscription.StudentId, subscription.PayerId, enrollment.GradeYearProfileId, newZone.Value, ChargeSourceType.ServiceAssignment, cancellationToken);
                subscription.ChargeId = charge.Id;
                await _db.SaveChangesAsync(cancellationToken);
            }

            await NotifyGuardiansAsync(subscription.StudentId, RouteChangedEventCode, new Dictionary<string, string> { ["StudentId"] = subscription.StudentId.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
        }

        public async Task SuspendForArrearsAsync(int subscriptionId, DateTime effectiveDate, int approvedByUserId, string reason, CancellationToken cancellationToken = default)
        {
            var subscription = await _db.TransportSubscriptions.SingleAsync(s => s.Id == subscriptionId, cancellationToken);
            var onTrip = await (
                from t in _db.Trips
                join l in _db.TripLogs on t.Id equals l.TripId
                where t.Status == TripStatus.InProgress && l.StudentId == subscription.StudentId && l.Event == TripLogEvent.Boarded
                select t.Id).AnyAsync(cancellationToken);
            if (onTrip)
            {
                throw new SuspensionMidTripException(subscription.StudentId);
            }

            _audit.Reason = reason;
            subscription.Status = TransportSubscriptionStatus.Suspended;
            subscription.SuspendedEffectiveDate = effectiveDate.Date;
            subscription.SuspensionReason = reason;
            subscription.SuspensionApprovedByUserId = approvedByUserId;
            await _db.SaveChangesAsync(cancellationToken);

            await NotifyGuardiansAsync(subscription.StudentId, SuspendedEventCode, new Dictionary<string, string>
            {
                ["EffectiveDate"] = effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            }, cancellationToken);
        }

        // ------------------------------------------------------------------ trips

        private async Task<List<int>> LoadRosterAsync(Route route, DateTime date, CancellationToken cancellationToken)
        {
            var stopIds = await _db.RouteStops.Where(s => s.RouteId == route.Id).Select(s => s.Id).ToListAsync(cancellationToken);
            var day = date.Date;
            var candidates = await _db.TransportSubscriptions
                .Where(s => s.StartDate <= day && (s.EndDate == null || s.EndDate >= day))
                .Where(s => s.Status == TransportSubscriptionStatus.Active || (s.Status == TransportSubscriptionStatus.Suspended && s.SuspendedEffectiveDate > day))
                .Select(s => new { s.StudentId, s.AmRouteStopId, s.PmRouteStopId })
                .ToListAsync(cancellationToken);
            return candidates
                .Where(s => route.Direction == RouteDirection.Am ? s.AmRouteStopId.HasValue && stopIds.Contains(s.AmRouteStopId.Value) : s.PmRouteStopId.HasValue && stopIds.Contains(s.PmRouteStopId.Value))
                .Select(s => s.StudentId).Distinct().ToList();
        }

        public async Task<Trip> OpenTripAsync(int routeId, DateTime date, int? substituteDriverId = null, int? substituteAttendantId = null, string? unroadworthyOverrideReason = null, CancellationToken cancellationToken = default)
        {
            var route = await _db.Routes.SingleAsync(r => r.Id == routeId, cancellationToken);
            if (await _db.Trips.AnyAsync(t => t.RouteId == routeId && t.Date == date.Date, cancellationToken))
            {
                throw new TripAlreadyOpenException(routeId, date);
            }

            var bus = await _db.Buses.SingleAsync(b => b.Id == route.BusId, cancellationToken);
            var documents = await _db.BusDocuments.Where(d => d.BusId == bus.Id).Select(d => new RoadworthinessEvaluator.DocumentInput(d.Kind, d.ExpiryDate)).ToListAsync(cancellationToken);
            var blockers = RoadworthinessEvaluator.Blockers(documents, date);
            SafetyEvent? overrideEvent = null;
            if (blockers.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(unroadworthyOverrideReason))
                {
                    throw new BusUnroadworthyException(bus.Id, blockers);
                }

                _audit.Reason = unroadworthyOverrideReason;
                overrideEvent = new SafetyEvent { Kind = SafetyEventKind.UnroadworthyOverride, OccurredAtUtc = _clock.UtcNow, Note = $"bus {bus.PlateNo}: {string.Join(", ", blockers)} — {unroadworthyOverrideReason}" };
            }

            var driverId = substituteDriverId ?? route.DriverId;
            var driver = await _db.TransportStaff.SingleAsync(s => s.Id == driverId, cancellationToken);
            if (driver.Kind != TransportStaffKind.Driver || !DriverEligibilityEvaluator.CanDrive(driver.LicenseClass, driver.LicenseExpiryDate, bus.RequiredLicenseClass, date))
            {
                throw new DriverNotEligibleException(driverId, bus.Id);
            }

            var roster = await LoadRosterAsync(route, date, cancellationToken);
            var trip = new Trip
            {
                RouteId = routeId, Date = date.Date, Direction = route.Direction, BusId = bus.Id, DriverId = driverId,
                AttendantId = substituteAttendantId ?? route.AttendantId, OpenedAtUtc = _clock.UtcNow, RosterCount = roster.Count,
            };
            _db.Trips.Add(trip);
            await _db.SaveChangesAsync(cancellationToken);

            if (overrideEvent != null)
            {
                overrideEvent.TripId = trip.Id;
                _db.SafetyEvents.Add(overrideEvent);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return trip;
        }

        private async Task<(Trip Trip, Route Route)> LoadOpenTripAsync(int tripId, int studentId, CancellationToken cancellationToken)
        {
            var trip = await _db.Trips.SingleAsync(t => t.Id == tripId, cancellationToken);
            if (trip.Status != TripStatus.InProgress)
            {
                throw new TripNotInProgressException(tripId);
            }

            var route = await _db.Routes.SingleAsync(r => r.Id == trip.RouteId, cancellationToken);
            var roster = await LoadRosterAsync(route, trip.Date, cancellationToken);
            if (!roster.Contains(studentId))
            {
                throw new StudentNotOnTripRosterException(tripId, studentId);
            }

            return (trip, route);
        }

        private async Task LogAsync(int tripId, int studentId, TripLogEvent evt, int actorUserId, string? receivedByName, bool handoverConfirmed, CancellationToken cancellationToken)
        {
            _db.TripLogs.Add(new TripLog { TripId = tripId, StudentId = studentId, Event = evt, AtUtc = _clock.UtcNow, ActorUserId = actorUserId, ReceivedByName = receivedByName, HandoverConfirmed = handoverConfirmed });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task LogBoardingAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default)
        {
            await LoadOpenTripAsync(tripId, studentId, cancellationToken);
            await LogAsync(tripId, studentId, TripLogEvent.Boarded, actorUserId, null, false, cancellationToken);
        }

        public async Task LogAlightingAsync(int tripId, int studentId, int actorUserId, bool handoverRequired = false, string? receivedByName = null, CancellationToken cancellationToken = default)
        {
            var (trip, _) = await LoadOpenTripAsync(tripId, studentId, cancellationToken);
            var confirmed = false;
            if (trip.Direction == RouteDirection.Pm && handoverRequired)
            {
                var subscription = await _db.TransportSubscriptions.SingleAsync(s => s.StudentId == studentId && s.Status != TransportSubscriptionStatus.Ended && s.AcademicYearId == _workingYear.AcademicYearId, cancellationToken);
                var authorized = !string.IsNullOrWhiteSpace(receivedByName) && await IsPickupAuthorizedAsync(studentId, receivedByName!, cancellationToken);
                if (!HandoverPolicy.IsAcceptable(handoverRequired, subscription.IsSelfReleaseAllowed, authorized))
                {
                    _db.SafetyEvents.Add(new SafetyEvent { TripId = tripId, StudentId = studentId, Kind = SafetyEventKind.UnauthorizedHandover, OccurredAtUtc = _clock.UtcNow, Note = receivedByName });
                    await _db.SaveChangesAsync(cancellationToken);
                    throw new HandoverNotAuthorizedException(studentId, receivedByName);
                }

                confirmed = true;
            }

            await LogAsync(tripId, studentId, TripLogEvent.Alighted, actorUserId, receivedByName, confirmed, cancellationToken);
        }

        private async Task<bool> IsPickupAuthorizedAsync(int studentId, string name, CancellationToken cancellationToken)
        {
            // BR-PAR-008: pickup-authorized guardians (StudentGuardianLink) and emergency contacts, matched by name in either language.
            var guardianIds = await _db.StudentGuardianLinks
                .Where(l => l.StudentId == studentId && l.IsPickupAuthorized && l.EffectiveToUtc == null)
                .Select(l => l.ParentId).ToListAsync(cancellationToken);
            if (await _db.Parents.AnyAsync(p => guardianIds.Contains(p.Id) && (p.NameAr == name || p.NameEn == name), cancellationToken))
            {
                return true;
            }

            return await _db.EmergencyContacts.AnyAsync(c => c.StudentId == studentId && c.IsPickupAuthorized && (c.NameAr == name || c.NameEn == name), cancellationToken);
        }

        public async Task DeclareAbsentAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default)
        {
            await LoadOpenTripAsync(tripId, studentId, cancellationToken);
            await LogAsync(tripId, studentId, TripLogEvent.AbsentDeclared, actorUserId, null, false, cancellationToken);
        }

        public async Task RecordNotBoardedAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default)
        {
            var (trip, _) = await LoadOpenTripAsync(tripId, studentId, cancellationToken);
            await LogAsync(tripId, studentId, TripLogEvent.NotBoarded, actorUserId, null, false, cancellationToken);
            _db.SafetyEvents.Add(new SafetyEvent { TripId = tripId, StudentId = studentId, Kind = SafetyEventKind.NotBoardedAm, OccurredAtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);

            // BR-TRN-005: immediate parent notification, urgent class (BR-NOT-004 quiet-hours bypass is the dispatcher's concern).
            await NotifyGuardiansAsync(studentId, NotBoardedEventCode, new Dictionary<string, string>
            {
                ["Date"] = trip.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["Urgent"] = "true",
            }, cancellationToken);
        }

        public async Task RecordNotCollectedAsync(int tripId, int studentId, int actorUserId, CancellationToken cancellationToken = default)
        {
            await LoadOpenTripAsync(tripId, studentId, cancellationToken);
            await LogAsync(tripId, studentId, TripLogEvent.NotCollected, actorUserId, null, false, cancellationToken);
            _db.SafetyEvents.Add(new SafetyEvent { TripId = tripId, StudentId = studentId, Kind = SafetyEventKind.NotCollectedPm, State = SafetyEventState.Escalated, OccurredAtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CloseTripAsync(int tripId, bool sweepConfirmed, CancellationToken cancellationToken = default)
        {
            var trip = await _db.Trips.SingleAsync(t => t.Id == tripId, cancellationToken);
            if (trip.Status != TripStatus.InProgress)
            {
                throw new TripNotInProgressException(tripId);
            }

            var route = await _db.Routes.SingleAsync(r => r.Id == trip.RouteId, cancellationToken);
            var roster = await LoadRosterAsync(route, trip.Date, cancellationToken);
            var logs = await _db.TripLogs.Where(l => l.TripId == tripId).ToListAsync(cancellationToken);
            var events = roster.Select(id => new TripCloseEvaluator.StudentEvents(id, logs.Where(l => l.StudentId == id).Select(l => l.Event).ToList())).ToList();
            if (!TripCloseEvaluator.CanClose(events, sweepConfirmed))
            {
                throw new TripNotClosableException(tripId, TripCloseEvaluator.UnresolvedStudents(events), sweepConfirmed);
            }

            trip.SweepConfirmed = true;
            trip.Status = TripStatus.Closed;
            trip.ClosedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> EscalateUnclosedTripsAsync(CancellationToken cancellationToken = default)
        {
            var today = _clock.UtcNow.Date;
            var stale = await _db.Trips.Where(t => t.Status == TripStatus.InProgress && t.Date < today).ToListAsync(cancellationToken);
            foreach (var trip in stale)
            {
                trip.Status = TripStatus.Escalated;
                _db.SafetyEvents.Add(new SafetyEvent { TripId = trip.Id, Kind = SafetyEventKind.UnclosedTrip, State = SafetyEventState.Escalated, OccurredAtUtc = _clock.UtcNow });
            }

            await _db.SaveChangesAsync(cancellationToken);
            return stale.Count;
        }

        public async Task ResolveSafetyEventAsync(int safetyEventId, string resolution, CancellationToken cancellationToken = default)
        {
            var safetyEvent = await _db.SafetyEvents.SingleAsync(e => e.Id == safetyEventId, cancellationToken);
            _audit.Reason = resolution;
            safetyEvent.State = SafetyEventState.Resolved;
            safetyEvent.ResolvedAtUtc = _clock.UtcNow;
            safetyEvent.Note = string.IsNullOrEmpty(safetyEvent.Note) ? resolution : $"{safetyEvent.Note} | {resolution}";
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task NotifyGuardiansAsync(int studentId, string eventCode, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
        {
            var parentIds = await _db.StudentGuardianLinks.Where(l => l.StudentId == studentId && l.EffectiveToUtc == null).Select(l => l.ParentId).ToListAsync(cancellationToken);
            var recipients = await _db.Parents
                .Where(p => parentIds.Contains(p.Id) && p.UserAccountId != null)
                .Select(p => new { p.UserAccountId, p.PreferredLanguage })
                .ToListAsync(cancellationToken);
            await _notifications.PublishAsync(eventCode, recipients.Select(r => new NotificationRecipient(r.UserAccountId!.Value, r.PreferredLanguage)).ToList(), payload, cancellationToken);
        }
    }
}
