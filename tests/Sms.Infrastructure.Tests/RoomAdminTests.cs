using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Classrooms;
using Sms.Domain.Grades;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Classrooms;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-104 (slice: Classrooms, doc/Modules/08, BR-ROM-001/002/004) over a
    /// real Sqlite-backed AppDbContext — E-104's final slice.
    /// </summary>
    public sealed class RoomAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _floorId;

        public RoomAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            var building = new Building { Name = new Sms.Domain.Common.LocalizedName("المبنى الرئيسي", "Main Building") };
            db.Buildings.Add(building);
            db.SaveChanges();
            var floor = new Floor { BuildingId = building.Id, Name = new Sms.Domain.Common.LocalizedName("الطابق الأرضي", "Ground Floor"), SequenceOrder = 0 };
            db.Floors.Add(floor);
            db.SaveChanges();
            _floorId = floor.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        // --- BR-ROM-001 catalog --------------------------------------------------

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public async Task Defining_a_room_links_it_to_its_floor()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);

            var room = await admin.DefineRoomAsync(_floorId, "101", "فصل ١٠١", "Room 101", roomTypeLookupId: 1, 30, 28, GenderPolicy.Mixed);

            Assert.Equal(_floorId, db.Rooms.Single(r => r.Id == room.Id).FloorId);
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public async Task A_duplicate_room_code_is_rejected()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            await admin.DefineRoomAsync(_floorId, "101", "فصل ١٠١", "Room 101", 1, 30, 28, GenderPolicy.Mixed);

            await Assert.ThrowsAsync<DuplicateRoomCodeException>(() =>
                admin.DefineRoomAsync(_floorId, "101", "فصل آخر", "Another Room", 1, 25, 22, GenderPolicy.Mixed));
        }

        // --- BR-ROM-002 capacity --------------------------------------------------

        [Fact]
        [BusinessRule("BR-ROM-002")]
        public async Task Exam_capacity_above_standard_capacity_is_rejected()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);

            await Assert.ThrowsAsync<InvalidRoomCapacityException>(() =>
                admin.DefineRoomAsync(_floorId, "101", "فصل ١٠١", "Room 101", 1, standardCapacity: 30, examCapacity: 31, GenderPolicy.Mixed));
        }

        // --- BR-ROM-004 availability -----------------------------------------------

        [Fact]
        [BusinessRule("BR-ROM-004")]
        public async Task A_booking_overlapping_a_maintenance_window_is_rejected()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var room = await admin.DefineRoomAsync(_floorId, "101", "فصل ١٠١", "Room 101", 1, 30, 28, GenderPolicy.Mixed);
            await admin.SetUnavailableAsync(room.Id, RoomAvailabilityReason.Maintenance, new DateTime(2026, 9, 1), new DateTime(2026, 9, 10));

            await Assert.ThrowsAsync<RoomUnavailableException>(() =>
                admin.RequestBookingAsync(room.Id, _tenant.AcademicYearId, "Science fair", new DateTime(2026, 9, 5), new DateTime(2026, 9, 6), requestedByUserId: 1));
        }

        [Fact]
        [BusinessRule("BR-ROM-004")]
        public async Task A_free_slot_booking_auto_approves()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var room = await admin.DefineRoomAsync(_floorId, "101", "فصل ١٠١", "Room 101", 1, 30, 28, GenderPolicy.Mixed);

            var booking = await admin.RequestBookingAsync(room.Id, _tenant.AcademicYearId, "Parent meeting", new DateTime(2026, 9, 15), new DateTime(2026, 9, 15, 2, 0, 0), 1);

            Assert.Equal(RoomBookingStatus.Approved, db.RoomBookings.Single(b => b.Id == booking.Id).Status);
        }

        // --- BR-ROM-005 features ---------------------------------------------------

        [Fact]
        [BusinessRule("BR-ROM-005")]
        public async Task Adding_a_feature_links_it_to_the_room()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var room = await admin.DefineRoomAsync(_floorId, "LAB1", "مختبر", "Lab 1", 2, 24, 20, GenderPolicy.Mixed);

            var feature = await admin.AddFeatureAsync(room.Id, featureLookupId: 5);

            Assert.Equal(room.Id, db.RoomFeatures.Single(f => f.Id == feature.Id).RoomId);
        }
    }
}
