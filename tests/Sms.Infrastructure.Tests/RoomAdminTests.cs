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

        // --- BR-ROM-001 the code the screen fills in --------------------------------

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public async Task The_suggested_code_names_every_floor_and_starts_at_one()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);

            var suggestions = await admin.SuggestRoomCodesAsync();

            Assert.Equal("A-001", suggestions[_floorId]);
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public async Task The_suggested_code_moves_on_once_it_is_used()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var first = (await admin.SuggestRoomCodesAsync())[_floorId];
            await admin.DefineRoomAsync(_floorId, first, "فصل", "Room", 1, 30, 28, GenderPolicy.Mixed);

            var second = (await admin.SuggestRoomCodesAsync())[_floorId];

            Assert.NotEqual(first, second);
            Assert.Equal("A-002", second);
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public async Task A_deactivated_rooms_code_is_never_suggested_again()
        {
            // The unique index does not forget a retired room's code. Reading the
            // taken codes through the soft-active filter would hand it out a second
            // time and turn the next save into a raw DbUpdateException.
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var room = await admin.DefineRoomAsync(_floorId, "A-001", "فصل", "Room", 1, 30, 28, GenderPolicy.Mixed);
            await admin.DeactivateRoomAsync(room.Id);

            var suggested = (await admin.SuggestRoomCodesAsync())[_floorId];

            Assert.Equal("A-002", suggested);
        }

        [Fact]
        [BusinessRule("BR-ROM-001")]
        public async Task A_deactivated_rooms_code_cannot_be_taken_by_a_new_room()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var room = await admin.DefineRoomAsync(_floorId, "A-001", "فصل", "Room", 1, 30, 28, GenderPolicy.Mixed);
            await admin.DeactivateRoomAsync(room.Id);

            await Assert.ThrowsAsync<DuplicateRoomCodeException>(() =>
                admin.DefineRoomAsync(_floorId, "A-001", "فصل آخر", "Another Room", 1, 25, 22, GenderPolicy.Mixed));
        }

        // --- edit / soft-delete of the building → floor → room tree ------------------

        [Fact]
        [BusinessRule("BR-ROM-002")]
        public async Task Editing_a_room_applies_the_code_and_capacity_rules()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var a = await admin.DefineRoomAsync(_floorId, "101", "فصل ١٠١", "Room 101", 1, 30, 28, GenderPolicy.Mixed);
            await admin.DefineRoomAsync(_floorId, "102", "فصل ١٠٢", "Room 102", 1, 30, 28, GenderPolicy.Mixed);

            var updated = await admin.UpdateRoomAsync(a.Id, _floorId, "101", "قاعة ١٠١", "Hall 101", 1, 40, 30, GenderPolicy.Boys);
            Assert.Equal(40, db.Rooms.Single(r => r.Id == a.Id).StandardCapacity);
            Assert.Equal(GenderPolicy.Boys, updated.WingTag);

            await Assert.ThrowsAsync<InvalidRoomCapacityException>(() => admin.UpdateRoomAsync(a.Id, _floorId, "101", "أ", "A", 1, 20, 25, GenderPolicy.Mixed));
            await Assert.ThrowsAsync<DuplicateRoomCodeException>(() => admin.UpdateRoomAsync(a.Id, _floorId, "102", "أ", "A", 1, 30, 20, GenderPolicy.Mixed));
        }

        [Fact]
        public async Task Removing_walks_the_tree_bottom_up_and_respects_sections_using_a_room()
        {
            using var db = CreateContext();
            var admin = new RoomAdmin(db);
            var floor = db.Floors.Single(f => f.Id == _floorId);
            var room = await admin.DefineRoomAsync(_floorId, "101", "فصل ١٠١", "Room 101", 1, 30, 28, GenderPolicy.Mixed);

            // floor with a room, building with a floor → refused
            await Assert.ThrowsAsync<RoomInUseException>(() => admin.DeactivateFloorAsync(_floorId));
            await Assert.ThrowsAsync<RoomInUseException>(() => admin.DeactivateBuildingAsync(floor.BuildingId));

            // room used by an active section → refused until the section moves
            var year = new Sms.Domain.Schools.AcademicYear { LabelAr = "ع", LabelEn = "2026-2027", HijriLabel = "h", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = Sms.Domain.Schools.AcademicYearStatus.Active };
            var stage = new Stage { Name = new Sms.Domain.Common.LocalizedName("م", "Stage"), SequenceOrder = 1 };
            db.AcademicYears.Add(year); db.Stages.Add(stage);
            await db.SaveChangesAsync();
            var grade = new GradeLevel { StageId = stage.Id, Code = "G1", Name = new Sms.Domain.Common.LocalizedName("ص", "Grade 1"), SequenceOrder = 1 };
            db.GradeLevels.Add(grade);
            await db.SaveChangesAsync();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            await db.SaveChangesAsync();
            db.Sections.Add(new Sms.Domain.Sections.Section { AcademicYearId = year.Id, GradeYearProfileId = profile.Id, NameAr = "أ", NameEn = "1-A", Capacity = 25, DefaultClassroomId = room.Id });
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<RoomInUseException>(() => admin.DeactivateRoomAsync(room.Id));
            var section = db.Sections.Single();
            section.DefaultClassroomId = null;
            await db.SaveChangesAsync();

            await admin.DeactivateRoomAsync(room.Id);
            await admin.DeactivateFloorAsync(_floorId);
            await admin.DeactivateBuildingAsync(floor.BuildingId);
            Assert.Empty(db.Rooms);
            Assert.Empty(db.Floors);
            Assert.Empty(db.Buildings);
            Assert.False(db.Rooms.IgnoreQueryFilters().Single(r => r.Id == room.Id).IsActive);
        }
    }
}
