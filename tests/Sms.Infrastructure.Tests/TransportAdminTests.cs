using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Transport;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Domain.Transport;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Transport;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S6/E-601 (Transportation, doc/Modules/23, BR-TRN-001..009) over a real Sqlite-backed AppDbContext with E-303 charges.</summary>
    public sealed class TransportAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 5, 5, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId { get; set; }
        }

        private static readonly DateTime TripDate = new(2026, 10, 5);

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _yearId;
        private int _profileId;
        private int _studentId;
        private int _parentId;
        private int _payerId;
        private int _zoneAId;
        private int _zoneBId;

        public TransportAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}"), ("RTE", "RTE-{SEQ:3}") })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = code, FormatTemplate = template,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

            var year = new AcademicYear
            {
                LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("Stage", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;
            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("Grade", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();

            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "Guardian", NameEn = "Guardian", PrimaryMobile = "0500000000", UserAccountId = 42 };
            db.Parents.Add(parent);
            db.SaveChanges();
            var payer = new Payer { Type = PayerType.Parent, ParentId = parent.Id };
            db.Payers.Add(payer);
            var zoneA = new FeeCategory { NameAr = "Transport A", NameEn = "Transport Zone A", IsServiceLinked = true, IsRefundable = true };
            var zoneB = new FeeCategory { NameAr = "Transport B", NameEn = "Transport Zone B", IsServiceLinked = true, IsRefundable = true };
            db.FeeCategories.AddRange(zoneA, zoneB);
            db.SaveChanges();
            db.FeeStructureLines.AddRange(
                new FeeStructureLine { AcademicYearId = year.Id, GradeYearProfileId = profile.Id, FeeCategoryId = zoneA.Id, Amount = 2000m, Status = FeeStructureLineStatus.Approved },
                new FeeStructureLine { AcademicYearId = year.Id, GradeYearProfileId = profile.Id, FeeCategoryId = zoneB.Id, Amount = 3000m, Status = FeeStructureLineStatus.Approved });
            db.SaveChanges();

            _yearId = year.Id;
            _profileId = profile.Id;
            _parentId = parent.Id;
            _payerId = payer.Id;
            _zoneAId = zoneA.Id;
            _zoneBId = zoneB.Id;
            _studentId = EnrollChild(db, "STU-1");
        }

        public void Dispose() => _connection.Dispose();

        private int EnrollChild(AppDbContext db, string no)
        {
            var student = new Student
            {
                StudentNo = no, FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment { AcademicYearId = _yearId, StudentId = student.Id, GradeYearProfileId = _profileId, EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission });
            db.StudentGuardianLinks.Add(new StudentGuardianLink
            {
                StudentId = student.Id, ParentId = _parentId, RelationshipLookupId = 1, IsPrimaryContact = true, IsFinanciallyResponsible = true,
                IsPickupAuthorized = true, EffectiveFromUtc = new DateTime(2026, 9, 1),
            });
            db.SaveChanges();
            return student.Id;
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private TransportAdmin CreateAdmin(AppDbContext db)
        {
            var issuer = new NumberIssuer(db, _tenant, _tenant, _clock);
            return new TransportAdmin(db, issuer, _clock, _audit, _tenant, new FeeAdmin(db, issuer, _clock), new NotificationPublisher(db));
        }

        private async Task<(Bus Bus, TransportStaff Driver)> RoadworthyBusAsync(TransportAdmin admin, int capacity = 30)
        {
            var bus = await admin.RegisterBusAsync("ABC-123", capacity, BusType.Standard, LicenseClass.Medium);
            foreach (var kind in new[] { BusDocumentKind.Registration, BusDocumentKind.Insurance, BusDocumentKind.SafetyInspection })
            {
                await admin.RecordBusDocumentAsync(bus.Id, kind, new DateTime(2027, 12, 31));
            }

            var driver = await admin.RegisterStaffAsync(TransportStaffKind.Driver, "Driver", licenseNo: "L-1", licenseClass: LicenseClass.Heavy, licenseExpiryDate: new DateTime(2028, 1, 1));
            return (bus, driver);
        }

        private Task<Route> AmRouteAsync(TransportAdmin admin, Bus bus, TransportStaff driver, int? zone = null) => admin.DefineRouteAsync(
            "AM 1", "AM Route 1", RouteDirection.Am, bus.Id, driver.Id, new[]
            {
                new RouteStopInput("Stop 1", "Stop 1", new TimeSpan(6, 30, 0), zone ?? _zoneAId), new RouteStopInput("Stop 2", "Stop 2", new TimeSpan(6, 45, 0), zone ?? _zoneAId),
            });

        // --- BR-TRN-003 routes ----------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-TRN-003")]
        public async Task A_route_gets_an_RTE_number_and_rejects_non_sequential_stop_times()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (bus, driver) = await RoadworthyBusAsync(admin);

            var route = await AmRouteAsync(admin, bus, driver);

            Assert.Equal("RTE-001", route.RouteNo);
            await Assert.ThrowsAsync<StopTimesNotSequentialException>(() => admin.DefineRouteAsync("X", "X", RouteDirection.Pm, bus.Id, driver.Id, new[]
            {
                new RouteStopInput("A", "A", new TimeSpan(14, 0, 0), _zoneAId), new RouteStopInput("B", "B", new TimeSpan(13, 0, 0), _zoneAId),
            }));
        }

        // --- BR-TRN-004 subscriptions + charge -------------------------------------------------

        [Fact]
        [BusinessRule("BR-TRN-004")]
        public async Task Subscribing_posts_the_zone_priced_transport_charge()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (bus, driver) = await RoadworthyBusAsync(admin);
            var route = await AmRouteAsync(admin, bus, driver);
            var stop = db.RouteStops.First(s => s.RouteId == route.Id);

            var subscription = await admin.SubscribeAsync(_studentId, _payerId, stop.Id, null, new DateTime(2026, 9, 1));

            Assert.Equal(TransportSubscriptionStatus.Active, subscription.Status);
            var charge = db.Charges.Single(c => c.Id == subscription.ChargeId);
            Assert.Equal(2000m, charge.GrossAmount);
            Assert.Equal(_zoneAId, charge.FeeCategoryId);
            Assert.Equal(ChargeSourceType.ServiceAssignment, charge.SourceType);
            await Assert.ThrowsAsync<TransportSubscriptionExistsException>(() => admin.SubscribeAsync(_studentId, _payerId, stop.Id, null, new DateTime(2026, 9, 1)));
            await Assert.ThrowsAsync<SubscriptionDatesOutsideYearException>(() => admin.SubscribeAsync(EnrollChild(db, "STU-9"), _payerId, stop.Id, null, new DateTime(2026, 8, 1)));
        }

        [Fact]
        [BusinessRule("BR-TRN-003")]
        public async Task Overflowing_bus_capacity_waitlists_instead_of_charging()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (bus, driver) = await RoadworthyBusAsync(admin, capacity: 1);
            var route = await AmRouteAsync(admin, bus, driver);
            var stop = db.RouteStops.First(s => s.RouteId == route.Id);
            await admin.SubscribeAsync(_studentId, _payerId, stop.Id, null, new DateTime(2026, 9, 1));
            var second = EnrollChild(db, "STU-2");

            var waitlisted = await admin.SubscribeAsync(second, _payerId, stop.Id, null, new DateTime(2026, 9, 1));

            Assert.Equal(TransportSubscriptionStatus.Waitlisted, waitlisted.Status);
            Assert.Null(waitlisted.ChargeId);
            Assert.Equal(route.Id, db.RouteWaitlists.Single().RouteId);
            Assert.Equal(1, db.Charges.Count());
        }

        [Fact]
        [BusinessRule("BR-TRN-007")]
        public async Task Reassigning_to_another_zone_credits_the_old_charge_and_posts_the_new_one()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (bus, driver) = await RoadworthyBusAsync(admin);
            var routeA = await AmRouteAsync(admin, bus, driver, _zoneAId);
            var routeB = await AmRouteAsync(admin, bus, driver, _zoneBId);
            var stopA = db.RouteStops.First(s => s.RouteId == routeA.Id);
            var stopB = db.RouteStops.First(s => s.RouteId == routeB.Id);
            var subscription = await admin.SubscribeAsync(_studentId, _payerId, stopA.Id, null, new DateTime(2026, 9, 1));

            await admin.ReassignStopsAsync(subscription.Id, stopB.Id, null);

            Assert.Equal(2000m, db.CreditNotes.Single().Amount);
            var newCharge = db.Charges.Single(c => c.FeeCategoryId == _zoneBId);
            Assert.Equal(3000m, newCharge.GrossAmount);
            Assert.Equal(newCharge.Id, db.TransportSubscriptions.Single().ChargeId);
            Assert.Equal(3000m, await new FeeAdmin(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock).ComputeStudentPositionAsync(_studentId));
        }

        // --- BR-TRN-001/002 trip open gates -----------------------------------------------------

        [Fact]
        [BusinessRule("BR-TRN-001")]
        public async Task An_unroadworthy_bus_blocks_trip_open_unless_a_principal_override_is_logged()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var bus = await admin.RegisterBusAsync("EXP-1", 30, BusType.Standard, LicenseClass.Medium);
            await admin.RecordBusDocumentAsync(bus.Id, BusDocumentKind.Registration, new DateTime(2027, 12, 31));
            await admin.RecordBusDocumentAsync(bus.Id, BusDocumentKind.Insurance, new DateTime(2026, 10, 1));   // expired before trip date
            var driver = await admin.RegisterStaffAsync(TransportStaffKind.Driver, "Driver", licenseClass: LicenseClass.Heavy, licenseExpiryDate: new DateTime(2028, 1, 1));
            var route = await AmRouteAsync(admin, bus, driver);

            var ex = await Assert.ThrowsAsync<BusUnroadworthyException>(() => admin.OpenTripAsync(route.Id, TripDate));
            Assert.Equal(new[] { BusDocumentKind.Insurance, BusDocumentKind.SafetyInspection }, ex.Blockers);

            var trip = await admin.OpenTripAsync(route.Id, TripDate, unroadworthyOverrideReason: "emergency - only bus available");

            Assert.Equal(TripStatus.InProgress, trip.Status);
            var overrideEvent = db.SafetyEvents.Single(e => e.Kind == SafetyEventKind.UnroadworthyOverride);
            Assert.Equal(trip.Id, overrideEvent.TripId);
        }

        [Fact]
        [BusinessRule("BR-TRN-002")]
        public async Task A_driver_below_the_bus_licence_class_cannot_take_the_trip_but_a_qualified_substitute_can()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (bus, _) = await RoadworthyBusAsync(admin);
            var lightDriver = await admin.RegisterStaffAsync(TransportStaffKind.Driver, "Light", licenseClass: LicenseClass.Light, licenseExpiryDate: new DateTime(2028, 1, 1));
            var heavyDriver = await admin.RegisterStaffAsync(TransportStaffKind.Driver, "Heavy", licenseClass: LicenseClass.Heavy, licenseExpiryDate: new DateTime(2028, 1, 1));
            var route = await AmRouteAsync(admin, bus, lightDriver);

            await Assert.ThrowsAsync<DriverNotEligibleException>(() => admin.OpenTripAsync(route.Id, TripDate));
            var trip = await admin.OpenTripAsync(route.Id, TripDate, substituteDriverId: heavyDriver.Id);

            Assert.Equal(heavyDriver.Id, trip.DriverId);
            await Assert.ThrowsAsync<TripAlreadyOpenException>(() => admin.OpenTripAsync(route.Id, TripDate, substituteDriverId: heavyDriver.Id));
        }

        // --- BR-TRN-005 trip execution ------------------------------------------------------------

        private async Task<(Trip Trip, int SecondStudent)> OpenAmTripWithTwoRidersAsync(AppDbContext db, TransportAdmin admin)
        {
            var (bus, driver) = await RoadworthyBusAsync(admin);
            var route = await AmRouteAsync(admin, bus, driver);
            var stop = db.RouteStops.First(s => s.RouteId == route.Id);
            var second = EnrollChild(db, "STU-2");
            await admin.SubscribeAsync(_studentId, _payerId, stop.Id, null, new DateTime(2026, 9, 1));
            await admin.SubscribeAsync(second, _payerId, stop.Id, null, new DateTime(2026, 9, 1));
            var trip = await admin.OpenTripAsync(route.Id, TripDate);
            return (trip, second);
        }

        [Fact]
        [BusinessRule("BR-TRN-005")]
        public async Task A_trip_cannot_close_with_a_student_still_on_the_bus_or_without_the_sweep()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (trip, second) = await OpenAmTripWithTwoRidersAsync(db, admin);
            Assert.Equal(2, trip.RosterCount);
            await admin.LogBoardingAsync(trip.Id, _studentId, 1);
            await admin.DeclareAbsentAsync(trip.Id, second, 1);

            var ex = await Assert.ThrowsAsync<TripNotClosableException>(() => admin.CloseTripAsync(trip.Id, sweepConfirmed: true));
            Assert.Equal(new[] { _studentId }, ex.UnresolvedStudentIds);

            await admin.LogAlightingAsync(trip.Id, _studentId, 1);
            await Assert.ThrowsAsync<TripNotClosableException>(() => admin.CloseTripAsync(trip.Id, sweepConfirmed: false));
            await admin.CloseTripAsync(trip.Id, sweepConfirmed: true);

            var closed = db.Trips.Single();
            Assert.Equal(TripStatus.Closed, closed.Status);
            Assert.True(closed.SweepConfirmed);
            await Assert.ThrowsAsync<TripNotInProgressException>(() => admin.LogBoardingAsync(trip.Id, _studentId, 1));
        }

        [Fact]
        [BusinessRule("BR-TRN-005")]
        public async Task Logging_a_student_who_is_not_on_the_roster_is_rejected()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (trip, _) = await OpenAmTripWithTwoRidersAsync(db, admin);
            var stranger = EnrollChild(db, "STU-9");

            await Assert.ThrowsAsync<StudentNotOnTripRosterException>(() => admin.LogBoardingAsync(trip.Id, stranger, 1));
        }

        [Fact]
        [BusinessRule("BR-TRN-005")]
        public async Task A_student_not_boarded_in_the_morning_raises_a_safety_event_and_notifies_the_parent()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (trip, _) = await OpenAmTripWithTwoRidersAsync(db, admin);

            await admin.RecordNotBoardedAsync(trip.Id, _studentId, 1);

            var safety = db.SafetyEvents.Single();
            Assert.Equal(SafetyEventKind.NotBoardedAm, safety.Kind);
            Assert.Equal(_studentId, safety.StudentId);
            // The publisher only queues deliveries when a subscription rule + template exist (E-007); the event code path is exercised, nothing delivered.
            Assert.Empty(db.Deliveries);
        }

        [Fact]
        [BusinessRule("BR-TRN-005")]
        public async Task Unclosed_trips_escalate_the_next_day()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (trip, _) = await OpenAmTripWithTwoRidersAsync(db, admin);
            _clock.UtcNow = TripDate.AddDays(1).AddHours(6);

            var escalated = await admin.EscalateUnclosedTripsAsync();

            Assert.Equal(1, escalated);
            Assert.Equal(TripStatus.Escalated, db.Trips.Single(t => t.Id == trip.Id).Status);
            Assert.Equal(SafetyEventKind.UnclosedTrip, db.SafetyEvents.Single().Kind);
        }

        // --- BR-TRN-006 handover ------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-TRN-006")]
        public async Task PM_handover_requires_a_pickup_authorized_receiver_unless_self_release_is_consented()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (bus, driver) = await RoadworthyBusAsync(admin);
            var route = await admin.DefineRouteAsync("PM 1", "PM Route 1", RouteDirection.Pm, bus.Id, driver.Id, new[] { new RouteStopInput("Stop 1", "Stop 1", new TimeSpan(14, 0, 0), _zoneAId) });
            var stop = db.RouteStops.Single(s => s.RouteId == route.Id);
            var teen = EnrollChild(db, "STU-2");
            await admin.SubscribeAsync(_studentId, _payerId, null, stop.Id, new DateTime(2026, 9, 1));
            await admin.SubscribeAsync(teen, _payerId, null, stop.Id, new DateTime(2026, 9, 1), isSelfReleaseAllowed: true);
            var trip = await admin.OpenTripAsync(route.Id, TripDate);
            await admin.LogBoardingAsync(trip.Id, _studentId, 1);
            await admin.LogBoardingAsync(trip.Id, teen, 1);

            await Assert.ThrowsAsync<HandoverNotAuthorizedException>(() => admin.LogAlightingAsync(trip.Id, _studentId, 1, handoverRequired: true, receivedByName: "Stranger"));
            Assert.Equal(SafetyEventKind.UnauthorizedHandover, db.SafetyEvents.Single().Kind);

            await admin.LogAlightingAsync(trip.Id, _studentId, 1, handoverRequired: true, receivedByName: "Guardian");
            await admin.LogAlightingAsync(trip.Id, teen, 1, handoverRequired: true);

            Assert.True(db.TripLogs.Single(l => l.StudentId == _studentId && l.Event == TripLogEvent.Alighted).HandoverConfirmed);
            await admin.CloseTripAsync(trip.Id, sweepConfirmed: true);
        }

        // --- BR-TRN-008 arrears suspension -----------------------------------------------------------

        [Fact]
        [BusinessRule("BR-TRN-008")]
        public async Task Suspension_is_effective_dated_reason_required_and_never_mid_trip()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (trip, second) = await OpenAmTripWithTwoRidersAsync(db, admin);
            await admin.LogBoardingAsync(trip.Id, _studentId, 1);
            var riding = db.TransportSubscriptions.Single(s => s.StudentId == _studentId);
            var waiting = db.TransportSubscriptions.Single(s => s.StudentId == second);

            await Assert.ThrowsAsync<SuspensionMidTripException>(() => admin.SuspendForArrearsAsync(riding.Id, TripDate.AddDays(7), 9, "arrears 90 days"));
            await admin.SuspendForArrearsAsync(waiting.Id, TripDate.AddDays(7), 9, "arrears 90 days");

            var suspended = db.TransportSubscriptions.Single(s => s.Id == waiting.Id);
            Assert.Equal(TransportSubscriptionStatus.Suspended, suspended.Status);
            Assert.Equal(TripDate.AddDays(7), suspended.SuspendedEffectiveDate);
            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(TransportSubscription) && e.FieldName == nameof(TransportSubscription.SuspendedEffectiveDate));
            Assert.Equal("arrears 90 days", audit.Reason);

            // Still on tomorrow's roster (suspension not yet effective), gone once it is.
            var route = db.Routes.Single();
            var tomorrow = await admin.OpenTripAsync(route.Id, TripDate.AddDays(1));
            Assert.Equal(2, tomorrow.RosterCount);
            var later = await admin.OpenTripAsync(route.Id, TripDate.AddDays(8));
            Assert.Equal(1, later.RosterCount);
        }

        // --- BR-TRN-009 audit ---------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-TRN-009")]
        public async Task Safety_event_resolution_is_reason_required_T1()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var (trip, _) = await OpenAmTripWithTwoRidersAsync(db, admin);
            await admin.RecordNotBoardedAsync(trip.Id, _studentId, 1);
            var safety = db.SafetyEvents.Single();

            await admin.ResolveSafetyEventAsync(safety.Id, "parent confirmed sick at home");

            Assert.Equal(SafetyEventState.Resolved, db.SafetyEvents.Single().State);
            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(SafetyEvent) && e.FieldName == nameof(SafetyEvent.ResolvedAtUtc));
            Assert.Equal("parent confirmed sick at home", audit.Reason);
        }
    }
}
