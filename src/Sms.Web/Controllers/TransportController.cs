using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Application.Transport;
using Sms.Domain.Security;
using Sms.Domain.Transport;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/23 §8 — the five transport screens the module doc named and E-501 left as engine
    /// only: fleet and documents, drivers and attendants, the route designer, the subscription desk,
    /// and the trip console, plus the safety register the last of those feeds.
    /// <para>
    /// Every rule these screens appear to enforce is enforced by <see cref="ITransportAdmin"/>: the
    /// roadworthiness block and its Principal override, the driver's licence class, sequential stop
    /// times, capacity and the waitlist, the zone re-pricing when a stop moves, and the sweep before a
    /// trip can close. The screens show what they can early — a bus with an expired document is
    /// marked before anyone picks it — and show the engine's own refusal when it comes.
    /// </para>
    /// <para>
    /// The trip console is the one screen here used under time pressure, at 07:00, on a phone, by
    /// somebody standing next to a bus. It is a roster and five buttons, and every one of them is a
    /// form post: no part of resolving a child depends on script having loaded.
    /// </para>
    /// </summary>
    [Route("transport")]
    public class TransportController : Controller
    {
        private readonly ITransportAdmin _transport;
        private readonly AppDbContext _db;
        private readonly ICurrentUser _user;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _year;
        private readonly Sms.Application.Audit.IAuditContext _audit;

        public TransportController(
            ITransportAdmin transport, AppDbContext db, ICurrentUser user, IClock clock,
            IWorkingYearContext year, Sms.Application.Audit.IAuditContext audit)
        {
            _transport = transport;
            _db = db;
            _user = user;
            _clock = clock;
            _year = year;
            _audit = audit;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        // ------------------------------------------------------------------ fleet

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Fleet, ActionVerb.View)]
        public async Task<IActionResult> Index()
        {
            var today = _clock.UtcNow.Date;
            var buses = await _db.Buses.AsNoTracking().OrderBy(b => b.PlateNo).ToListAsync();
            var busIds = buses.Select(b => b.Id).ToList();
            var documents = await _db.BusDocuments.AsNoTracking()
                .Where(d => busIds.Contains(d.BusId))
                .ToListAsync();

            // One row per (bus, kind): the latest expiry wins, which is what renewing a document
            // means — a new row, not an edit, so the history of what was valid when survives.
            var rows = buses.Select(bus =>
            {
                var mine = documents.Where(d => d.BusId == bus.Id).ToList();
                var latest = mine
                    .GroupBy(d => d.Kind)
                    .ToDictionary(g => g.Key, g => g.Max(d => d.ExpiryDate));

                var blockers = RoadworthinessEvaluator.Blockers(
                    latest.Select(kv => new RoadworthinessEvaluator.DocumentInput(kv.Key, kv.Value)).ToList(), today);

                return new BusRowViewModel
                {
                    Bus = bus,
                    Documents = latest,
                    Blockers = blockers,
                    RouteCount = 0,
                };
            }).ToList();

            var routeCounts = await _db.Routes.AsNoTracking()
                .GroupBy(r => r.BusId)
                .Select(g => new { BusId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BusId, x => x.Count);
            foreach (var row in rows)
            {
                row.RouteCount = routeCounts.TryGetValue(row.Bus.Id, out var n) ? n : 0;
            }

            return View(new FleetViewModel { Buses = rows, Today = today });
        }

        [HttpPost("buses")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Fleet, ActionVerb.Create)]
        public async Task<IActionResult> RegisterBus(string plateNo, int capacity, BusType type, LicenseClass requiredLicenseClass)
        {
            try
            {
                await _transport.RegisterBusAsync(plateNo, capacity, type, requiredLicenseClass, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("buses/{id:int}/documents")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Fleet, ActionVerb.Edit)]
        public async Task<IActionResult> RecordDocument(int id, BusDocumentKind kind, DateTime expiryDate)
        {
            try
            {
                await _transport.RecordBusDocumentAsync(id, kind, expiryDate, cancellationToken: HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("buses/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Fleet, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateBus(int id, string? reason)
        {
            _audit.Reason = reason;
            var bus = await _db.Buses.SingleOrDefaultAsync(b => b.Id == id);
            if (bus == null)
            {
                return NotFound();
            }

            // Refused rather than cascaded: a bus withdrawn from service while routes still name it
            // would leave those routes unopenable tomorrow morning with no explanation on this screen.
            if (await _db.Routes.AnyAsync(r => r.BusId == id))
            {
                TempData["Error"] = IsArabic
                    ? "لا يمكن تعطيل حافلة ما زالت مُسنَدة إلى مسار — أعد إسناد المسار أولاً."
                    : "This bus is still assigned to a route — reassign the route first.";
                return RedirectToAction(nameof(Index));
            }

            bus.IsActive = false;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------------ staff

        [HttpGet("staff")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Staff, ActionVerb.View)]
        public async Task<IActionResult> Staff()
        {
            var today = _clock.UtcNow.Date;
            var staff = await _db.TransportStaff.AsNoTracking()
                .OrderBy(s => s.Kind).ThenBy(s => s.DisplayName)
                .ToListAsync();

            var driving = await _db.Routes.AsNoTracking()
                .GroupBy(r => r.DriverId)
                .Select(g => new { DriverId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.DriverId, x => x.Count);

            return View(new TransportStaffViewModel
            {
                Staff = staff,
                Today = today,
                RouteCountByStaffId = driving,
            });
        }

        [HttpPost("staff")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Staff, ActionVerb.Create)]
        public async Task<IActionResult> RegisterStaff(
            TransportStaffKind kind, string displayName, int? employeeId, string? contractorName,
            string? licenseNo, LicenseClass? licenseClass, DateTime? licenseExpiryDate)
        {
            try
            {
                await _transport.RegisterStaffAsync(
                    kind, displayName, employeeId, contractorName, licenseNo, licenseClass, licenseExpiryDate,
                    HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Staff));
        }

        /// <summary>
        /// Renewing a licence is an edit, not a second person: the route and every trip that named
        /// this driver still mean the same human being.
        /// </summary>
        [HttpPost("staff/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Staff, ActionVerb.Edit)]
        public async Task<IActionResult> UpdateStaff(
            int id, string displayName, string? licenseNo, LicenseClass? licenseClass, DateTime? licenseExpiryDate, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _transport.UpdateStaffAsync(
                    id, displayName, licenseNo, licenseClass, licenseExpiryDate, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Staff));
        }

        [HttpPost("staff/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Staff, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateStaff(int id, string? reason)
        {
            _audit.Reason = reason;
            var person = await _db.TransportStaff.SingleOrDefaultAsync(s => s.Id == id);
            if (person == null)
            {
                return NotFound();
            }

            if (await _db.Routes.AnyAsync(r => r.DriverId == id || r.AttendantId == id))
            {
                TempData["Error"] = IsArabic
                    ? "الشخص ما زال مُسنَداً إلى مسار — أعد الإسناد أولاً."
                    : "This person is still assigned to a route — reassign it first.";
                return RedirectToAction(nameof(Staff));
            }

            person.IsActive = false;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return RedirectToAction(nameof(Staff));
        }

        // ------------------------------------------------------------------ routes

        [HttpGet("routes")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Routes, ActionVerb.View)]
        public async Task<IActionResult> Routes()
        {
            var routes = await _db.Routes.AsNoTracking()
                .Include(r => r.Stops)
                .OrderBy(r => r.Direction).ThenBy(r => r.RouteNo)
                .ToListAsync();

            return View(new RoutesViewModel
            {
                Routes = routes,
                Buses = await ActiveBusesAsync(),
                Drivers = await ActiveStaffAsync(TransportStaffKind.Driver),
                Attendants = await ActiveStaffAsync(TransportStaffKind.Attendant),
                Riders = await RidersByRouteAsync(),
                FeeCategories = await TransportFeeCategoriesAsync(),
            });
        }

        [HttpGet("routes/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Routes, ActionVerb.View)]
        public async Task<IActionResult> Route(int id)
        {
            var route = await _db.Routes.AsNoTracking()
                .Include(r => r.Stops)
                .SingleOrDefaultAsync(r => r.Id == id);
            if (route == null)
            {
                return NotFound();
            }

            var bus = await _db.Buses.AsNoTracking().SingleOrDefaultAsync(b => b.Id == route.BusId);
            var riders = await RidersByRouteAsync();

            return View(new RouteDetailViewModel
            {
                Route = route,
                Bus = bus,
                Driver = await _db.TransportStaff.AsNoTracking().SingleOrDefaultAsync(s => s.Id == route.DriverId),
                Attendant = route.AttendantId is { } aid
                    ? await _db.TransportStaff.AsNoTracking().SingleOrDefaultAsync(s => s.Id == aid)
                    : null,
                RiderCount = riders.TryGetValue(route.Id, out var n) ? n : 0,
                FeeCategories = await TransportFeeCategoriesAsync(),
                Buses = await ActiveBusesAsync(),
                Drivers = await ActiveStaffAsync(TransportStaffKind.Driver),
                Attendants = await ActiveStaffAsync(TransportStaffKind.Attendant),
            });
        }

        /// <summary>
        /// Defines a route and its stops in one post. The stops arrive as parallel arrays because the
        /// designer is a repeating row the browser adds and removes without a round trip; the engine
        /// checks that their times ascend (BR-TRN-003) and refuses the whole route if they do not,
        /// which is why a half-saved route is not a state this screen can produce.
        /// </summary>
        [HttpPost("routes")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Routes, ActionVerb.Create)]
        public async Task<IActionResult> DefineRoute(
            string nameAr, string nameEn, RouteDirection direction, int busId, int driverId, int? attendantId,
            string[]? stopNameAr, string[]? stopNameEn, string[]? stopTime, int[]? stopFeeCategoryId)
        {
            var stops = BuildStops(stopNameAr, stopNameEn, stopTime, stopFeeCategoryId);
            if (stops.Count == 0)
            {
                TempData["Error"] = IsArabic ? "المسار يحتاج محطة واحدة على الأقل." : "A route needs at least one stop.";
                return RedirectToAction(nameof(Routes));
            }

            try
            {
                var route = await _transport.DefineRouteAsync(
                    nameAr, nameEn, direction, busId, driverId, stops, attendantId, HttpContext.RequestAborted);
                return RedirectToAction(nameof(Route), new { id = route.Id });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Routes));
            }
        }

        /// <summary>
        /// The standing bus and crew. What <c>DeactivateBus</c> tells the user to do before
        /// withdrawing a bus, and what a driver leaving in March needs.
        /// </summary>
        [HttpPost("routes/{id:int}/crew")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Routes, ActionVerb.Edit)]
        public async Task<IActionResult> ReassignCrew(int id, int busId, int driverId, int? attendantId, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _transport.ReassignRouteCrewAsync(id, busId, driverId, attendantId, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Route), new { id });
        }

        [HttpPost("routes/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Routes, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateRoute(int id, string? reason)
        {
            _audit.Reason = reason;
            var route = await _db.Routes.SingleOrDefaultAsync(r => r.Id == id);
            if (route == null)
            {
                return NotFound();
            }

            var stopIds = await _db.RouteStops.Where(s => s.RouteId == id).Select(s => s.Id).ToListAsync();
            var riders = await _db.TransportSubscriptions.CountAsync(s =>
                s.Status == TransportSubscriptionStatus.Active
                && ((s.AmRouteStopId != null && stopIds.Contains(s.AmRouteStopId.Value))
                    || (s.PmRouteStopId != null && stopIds.Contains(s.PmRouteStopId.Value))));

            if (riders > 0)
            {
                TempData["Error"] = IsArabic
                    ? $"المسار ما زال يحمل {riders} مشترِكاً — أنهِ اشتراكاتهم أو انقلها أولاً."
                    : $"This route still carries {riders} rider(s) — end or move their subscriptions first.";
                return RedirectToAction(nameof(Routes));
            }

            route.IsActive = false;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return RedirectToAction(nameof(Routes));
        }

        // ------------------------------------------------------------------ subscriptions

        [HttpGet("subscriptions")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Subscriptions, ActionVerb.View)]
        public async Task<IActionResult> Subscriptions(TransportSubscriptionStatus? status = null, string? q = null)
        {
            var query = _db.TransportSubscriptions.AsNoTracking().Where(s => s.AcademicYearId == _year.AcademicYearId);
            if (status is { } wanted)
            {
                query = query.Where(s => s.Status == wanted);
            }

            var subscriptions = await query.OrderByDescending(s => s.StartDate).Take(500).ToListAsync();
            var studentIds = subscriptions.Select(s => s.StudentId).Distinct().ToList();

            var students = await _db.Students.AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.StudentNo, NameAr = s.FirstNameAr + " " + s.FatherNameAr + " " + s.FamilyNameAr, NameEn = s.FirstNameEn + " " + s.FatherNameEn + " " + s.FamilyNameEn })
                .ToListAsync();
            var byStudent = students.ToDictionary(
                s => s.Id, s => new StudentLabel(s.StudentNo, s.NameAr, s.NameEn));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                var matching = new HashSet<int>(students
                    .Where(s => s.StudentNo.Contains(term, StringComparison.OrdinalIgnoreCase)
                                || s.NameAr.Contains(term) || s.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Id));
                subscriptions = subscriptions.Where(s => matching.Contains(s.StudentId)).ToList();
            }

            return View(new SubscriptionsViewModel
            {
                Subscriptions = subscriptions,
                Students = byStudent,
                Stops = await StopLabelsAsync(),
                Status = status,
                Search = q,
            });
        }

        [HttpPost("subscriptions")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Subscriptions, ActionVerb.Create)]
        public async Task<IActionResult> Subscribe(
            int studentId, int payerId, int? amRouteStopId, int? pmRouteStopId,
            DateTime startDate, DateTime? endDate, bool isSelfReleaseAllowed)
        {
            try
            {
                await _transport.SubscribeAsync(
                    studentId, payerId, amRouteStopId, pmRouteStopId, startDate, endDate, isSelfReleaseAllowed,
                    HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Subscriptions));
        }

        [HttpPost("subscriptions/{id:int}/stops")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Subscriptions, ActionVerb.Edit)]
        public async Task<IActionResult> ReassignStops(int id, int? amRouteStopId, int? pmRouteStopId, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _transport.ReassignStopsAsync(id, amRouteStopId, pmRouteStopId, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Subscriptions));
        }

        [HttpPost("subscriptions/{id:int}/end")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Subscriptions, ActionVerb.Deactivate)]
        public async Task<IActionResult> EndSubscription(int id, DateTime endDate, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _transport.EndSubscriptionAsync(id, endDate, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Subscriptions));
        }

        /// <summary>
        /// BR-TRN-008. Approve, not Edit: stopping a child's ride over money is the Principal's
        /// decision, it is effective-dated, and the reason is kept.
        /// </summary>
        [HttpPost("subscriptions/{id:int}/suspend")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Subscriptions, ActionVerb.Approve)]
        public async Task<IActionResult> SuspendSubscription(int id, DateTime effectiveDate, string reason)
        {
            _audit.Reason = reason;
            try
            {
                await _transport.SuspendForArrearsAsync(id, effectiveDate, _user.UserId, reason, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Subscriptions));
        }

        // ------------------------------------------------------------------ trips

        [HttpGet("trips")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Trips, ActionVerb.View)]
        public async Task<IActionResult> Trips(DateTime? date = null)
        {
            var day = (date ?? _clock.UtcNow).Date;
            var trips = await _db.Trips.AsNoTracking()
                .Where(t => t.Date == day)
                .OrderBy(t => t.Direction)
                .ToListAsync();

            var routes = await _db.Routes.AsNoTracking().Include(r => r.Stops).ToListAsync();
            var openRouteIds = new HashSet<int>(trips.Select(t => t.RouteId));

            return View(new TripsViewModel
            {
                Date = day,
                Trips = trips,
                Routes = routes,
                RoutesWithoutATrip = routes.Where(r => r.IsActive && !openRouteIds.Contains(r.Id)).ToList(),
                Drivers = await ActiveStaffAsync(TransportStaffKind.Driver),
                Attendants = await ActiveStaffAsync(TransportStaffKind.Attendant),
                ResolvedByTrip = await ResolvedCountsAsync(trips.Select(t => t.Id).ToList()),
            });
        }

        [HttpPost("trips")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Trips, ActionVerb.Post)]
        public async Task<IActionResult> OpenTrip(
            int routeId, DateTime date, int? substituteDriverId, int? substituteAttendantId, string? unroadworthyOverrideReason)
        {
            _audit.Reason = unroadworthyOverrideReason;
            try
            {
                var trip = await _transport.OpenTripAsync(
                    routeId, date, substituteDriverId, substituteAttendantId, unroadworthyOverrideReason,
                    HttpContext.RequestAborted);
                return RedirectToAction(nameof(Trip), new { id = trip.Id });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Trips), new { date });
            }
        }

        [HttpGet("trips/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Trips, ActionVerb.View)]
        public async Task<IActionResult> Trip(int id)
        {
            var trip = await _db.Trips.AsNoTracking().Include(t => t.Logs).SingleOrDefaultAsync(t => t.Id == id);
            if (trip == null)
            {
                return NotFound();
            }

            var route = await _db.Routes.AsNoTracking().Include(r => r.Stops).SingleOrDefaultAsync(r => r.Id == trip.RouteId);
            var stopIds = route?.Stops.Select(s => s.Id).ToList() ?? new List<int>();

            // The roster is recomputed from the subscriptions rather than stored per student: the
            // engine builds it the same way at open, and a child subscribed at 07:10 should appear on
            // the screen the driver is holding rather than in tomorrow's.
            var subscriptions = await _db.TransportSubscriptions.AsNoTracking()
                .Where(s => s.AcademicYearId == trip.SchoolId * 0 + s.AcademicYearId
                            && s.Status == TransportSubscriptionStatus.Active
                            && s.StartDate <= trip.Date
                            && (s.EndDate == null || s.EndDate >= trip.Date)
                            && ((trip.Direction == RouteDirection.Am && s.AmRouteStopId != null && stopIds.Contains(s.AmRouteStopId.Value))
                                || (trip.Direction == RouteDirection.Pm && s.PmRouteStopId != null && stopIds.Contains(s.PmRouteStopId.Value))))
                .ToListAsync();

            var studentIds = subscriptions.Select(s => s.StudentId).Distinct().ToList();
            var students = await _db.Students.AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.StudentNo, NameAr = s.FirstNameAr + " " + s.FatherNameAr + " " + s.FamilyNameAr, NameEn = s.FirstNameEn + " " + s.FatherNameEn + " " + s.FamilyNameEn })
                .ToListAsync();

            var logsByStudent = trip.Logs
                .GroupBy(l => l.StudentId)
                .ToDictionary(g => g.Key, g => g.OrderBy(l => l.AtUtc).Select(l => l.Event).ToList());

            var roster = subscriptions
                .Select(s => new TripRosterRow
                {
                    StudentId = s.StudentId,
                    Label = students.Where(x => x.Id == s.StudentId)
                        .Select(x => new StudentLabel(x.StudentNo, x.NameAr, x.NameEn))
                        .FirstOrDefault() ?? new StudentLabel("?", "?", "?"),
                    Events = logsByStudent.TryGetValue(s.StudentId, out var events) ? events : new List<TripLogEvent>(),
                    IsSelfReleaseAllowed = s.IsSelfReleaseAllowed,
                })
                .OrderBy(r => IsArabic ? r.Label.NameAr : r.Label.NameEn)
                .ToList();

            return View(new TripDetailViewModel
            {
                Trip = trip,
                Route = route,
                Roster = roster,
                Unresolved = roster.Count(r => !TripCloseEvaluator.IsResolved(r.Events)),
            });
        }

        /// <summary>
        /// One action for the five roster buttons. They differ only in which engine call they make and
        /// what the engine then does with it — routing them separately would be five near-identical
        /// methods whose only real difference is a string in the URL.
        /// </summary>
        [HttpPost("trips/{id:int}/log")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Trips, ActionVerb.Edit)]
        public async Task<IActionResult> LogTripEvent(
            int id, int studentId, TripLogEvent tripEvent, bool handoverRequired, string? receivedByName)
        {
            try
            {
                switch (tripEvent)
                {
                    case TripLogEvent.Boarded:
                        await _transport.LogBoardingAsync(id, studentId, _user.UserId, HttpContext.RequestAborted);
                        break;
                    case TripLogEvent.Alighted:
                        await _transport.LogAlightingAsync(
                            id, studentId, _user.UserId, handoverRequired, receivedByName, HttpContext.RequestAborted);
                        break;
                    case TripLogEvent.AbsentDeclared:
                        await _transport.DeclareAbsentAsync(id, studentId, _user.UserId, HttpContext.RequestAborted);
                        break;
                    case TripLogEvent.NotBoarded:
                        await _transport.RecordNotBoardedAsync(id, studentId, _user.UserId, HttpContext.RequestAborted);
                        break;
                    case TripLogEvent.NotCollected:
                        await _transport.RecordNotCollectedAsync(id, studentId, _user.UserId, HttpContext.RequestAborted);
                        break;
                    default:
                        return BadRequest();
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Trip), new { id });
        }

        [HttpPost("trips/{id:int}/close")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Trips, ActionVerb.Approve)]
        public async Task<IActionResult> CloseTrip(int id, bool sweepConfirmed)
        {
            try
            {
                await _transport.CloseTripAsync(id, sweepConfirmed, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Trip), new { id });
        }

        // ------------------------------------------------------------------ safety

        [HttpGet("safety")]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Safety, ActionVerb.View)]
        public async Task<IActionResult> Safety(SafetyEventState? state = null)
        {
            var query = _db.SafetyEvents.AsNoTracking();
            query = state is { } wanted
                ? query.Where(e => e.State == wanted)
                // Open and escalated first by default: a resolved register is history, and the
                // unresolved rows are the reason anybody opens this screen.
                : query.Where(e => e.State != SafetyEventState.Resolved);

            var events = await query.OrderByDescending(e => e.OccurredAtUtc).Take(300).ToListAsync();
            var studentIds = events.Where(e => e.StudentId != null).Select(e => e.StudentId!.Value).Distinct().ToList();

            var students = await _db.Students.AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.StudentNo, NameAr = s.FirstNameAr + " " + s.FatherNameAr + " " + s.FamilyNameAr, NameEn = s.FirstNameEn + " " + s.FatherNameEn + " " + s.FamilyNameEn })
                .ToDictionaryAsync(s => s.Id, s => new StudentLabel(s.StudentNo, s.NameAr, s.NameEn));

            return View(new SafetyViewModel { Events = events, Students = students, State = state });
        }

        [HttpPost("safety/{id:int}/resolve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Safety, ActionVerb.Approve)]
        public async Task<IActionResult> ResolveSafetyEvent(int id, string resolution)
        {
            _audit.Reason = resolution;
            try
            {
                await _transport.ResolveSafetyEventAsync(id, resolution, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Safety));
        }

        // ------------------------------------------------------------------ shared lookups

        private static List<RouteStopInput> BuildStops(
            string[]? nameAr, string[]? nameEn, string[]? time, int[]? feeCategoryId)
        {
            var stops = new List<RouteStopInput>();
            if (nameAr == null || nameEn == null || time == null || feeCategoryId == null)
            {
                return stops;
            }

            var count = new[] { nameAr.Length, nameEn.Length, time.Length, feeCategoryId.Length }.Min();
            for (var i = 0; i < count; i++)
            {
                // A row the user added and left blank is not a stop. Dropped here rather than refused,
                // because the designer always carries one empty row at the bottom.
                if (string.IsNullOrWhiteSpace(nameAr[i]) && string.IsNullOrWhiteSpace(nameEn[i]))
                {
                    continue;
                }

                if (!TimeSpan.TryParse(time[i], CultureInfo.InvariantCulture, out var at))
                {
                    continue;
                }

                stops.Add(new RouteStopInput(nameAr[i], nameEn[i], at, feeCategoryId[i]));
            }

            return stops;
        }

        private Task<List<Bus>> ActiveBusesAsync() =>
            _db.Buses.AsNoTracking().OrderBy(b => b.PlateNo).ToListAsync();

        private Task<List<TransportStaff>> ActiveStaffAsync(TransportStaffKind kind) =>
            _db.TransportStaff.AsNoTracking().Where(s => s.Kind == kind).OrderBy(s => s.DisplayName).ToListAsync();

        /// <summary>Active riders per route, for the capacity meter every route screen shows.</summary>
        private async Task<Dictionary<int, int>> RidersByRouteAsync()
        {
            var stops = await _db.RouteStops.AsNoTracking().Select(s => new { s.Id, s.RouteId }).ToListAsync();
            var routeByStop = stops.ToDictionary(s => s.Id, s => s.RouteId);

            var subscriptions = await _db.TransportSubscriptions.AsNoTracking()
                .Where(s => s.Status == TransportSubscriptionStatus.Active && s.AcademicYearId == _year.AcademicYearId)
                .Select(s => new { s.AmRouteStopId, s.PmRouteStopId })
                .ToListAsync();

            var counts = new Dictionary<int, int>();
            foreach (var stopId in subscriptions
                         .SelectMany(s => new[] { s.AmRouteStopId, s.PmRouteStopId })
                         .Where(id => id != null)
                         .Select(id => id!.Value))
            {
                if (routeByStop.TryGetValue(stopId, out var routeId))
                {
                    counts[routeId] = counts.TryGetValue(routeId, out var n) ? n + 1 : 1;
                }
            }

            return counts;
        }

        private async Task<IReadOnlyList<StopLabel>> StopLabelsAsync()
        {
            var routes = await _db.Routes.AsNoTracking().Include(r => r.Stops).ToListAsync();
            return routes
                .SelectMany(r => r.Stops.OrderBy(s => s.SequenceNumber).Select(s => new StopLabel(
                    s.Id, r.Id, r.RouteNo, r.Direction, s.SequenceNumber, s.NameAr, s.NameEn, s.ScheduledTime)))
                .ToList();
        }

        private async Task<Dictionary<int, int>> ResolvedCountsAsync(IReadOnlyList<int> tripIds)
        {
            if (tripIds.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var logs = await _db.TripLogs.AsNoTracking()
                .Where(l => tripIds.Contains(l.TripId))
                .Select(l => new { l.TripId, l.StudentId, l.Event })
                .ToListAsync();

            return logs
                .GroupBy(l => l.TripId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(l => l.StudentId)
                        .Count(s => TripCloseEvaluator.IsResolved(s.Select(l => l.Event).ToList())));
        }

        /// <summary>
        /// The fee categories a stop may be priced in. Zone pricing lives in the fee structure
        /// (doc/Modules/23 §7), so this offers the transport categories rather than inventing a
        /// second place to set a price.
        /// </summary>
        private async Task<IReadOnlyList<FeeCategoryLabel>> TransportFeeCategoriesAsync() =>
            await _db.FeeCategories.AsNoTracking()
                .OrderBy(c => c.NameEn)
                .Select(c => new FeeCategoryLabel(c.Id, c.NameAr, c.NameEn))
                .ToListAsync();
    }
}
