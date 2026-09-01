using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Classrooms;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.Domain.Classrooms;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Classrooms
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class RoomAdmin : IRoomAdmin
    {
        private readonly AppDbContext _db;

        public RoomAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Building> DefineBuildingAsync(string nameAr, string nameEn, CancellationToken cancellationToken = default)
        {
            var building = new Building { Name = new LocalizedName(nameAr, nameEn) };
            _db.Buildings.Add(building);

            await _db.SaveChangesAsync(cancellationToken);
            return building;
        }

        public async Task<Floor> DefineFloorAsync(int buildingId, string nameAr, string nameEn, int sequenceOrder, CancellationToken cancellationToken = default)
        {
            var floor = new Floor { BuildingId = buildingId, Name = new LocalizedName(nameAr, nameEn), SequenceOrder = sequenceOrder };
            _db.Floors.Add(floor);

            await _db.SaveChangesAsync(cancellationToken);
            return floor;
        }

        public async Task<Room> DefineRoomAsync(
            int floorId, string code, string nameAr, string nameEn, int roomTypeLookupId,
            int standardCapacity, int examCapacity, GenderPolicy wingTag, CancellationToken cancellationToken = default)
        {
            if (!RoomCapacityValidator.IsValidCapacity(standardCapacity, examCapacity))
            {
                throw new InvalidRoomCapacityException();
            }

            if (await IsCodeTakenAsync(code, null, cancellationToken))
            {
                throw new DuplicateRoomCodeException(code);
            }

            var room = new Room
            {
                FloorId = floorId,
                Code = code,
                Name = new LocalizedName(nameAr, nameEn),
                RoomTypeLookupId = roomTypeLookupId,
                StandardCapacity = standardCapacity,
                ExamCapacity = examCapacity,
                WingTag = wingTag,
            };
            _db.Rooms.Add(room);

            await _db.SaveChangesAsync(cancellationToken);
            return room;
        }

        public async Task<IReadOnlyDictionary<int, string>> SuggestRoomCodesAsync(CancellationToken cancellationToken = default)
        {
            var schoolId = _db.CurrentSchoolId;

            // Lookups, not pickers: read through IgnoreQueryFilters (with SchoolId
            // restated, since ignoring the filters drops tenant scoping with them) so
            // a floor under a retired building still gets a code, the building letters
            // do not shift under the existing rooms the day one is deactivated, and a
            // deactivated room's code — which the unique index still holds — is not
            // handed out a second time.
            var buildingIds = await _db.Buildings.IgnoreQueryFilters().AsNoTracking()
                .Where(b => b.SchoolId == schoolId).OrderBy(b => b.Id).Select(b => b.Id)
                .ToListAsync(cancellationToken);

            var floors = await _db.Floors.IgnoreQueryFilters().AsNoTracking()
                .Where(f => f.SchoolId == schoolId).Select(f => new { f.Id, f.BuildingId, f.SequenceOrder })
                .ToListAsync(cancellationToken);

            var takenCodes = await _db.Rooms.IgnoreQueryFilters().AsNoTracking()
                .Where(r => r.SchoolId == schoolId).Select(r => r.Code)
                .ToListAsync(cancellationToken);

            return floors.ToDictionary(
                f => f.Id,
                f => RoomCodeGenerator.Next(buildingIds.IndexOf(f.BuildingId) + 1, f.SequenceOrder, takenCodes));
        }

        /// <summary>
        /// BR-ROM-001 across the whole school. Deliberately ignores the soft-active
        /// filter: a deactivated room keeps its row and the unique index keeps its
        /// code, so reading through the filter would report a code free and then let
        /// SaveChanges fail on the index instead of raising the translated refusal.
        /// </summary>
        private async Task<bool> IsCodeTakenAsync(string code, int? exceptRoomId, CancellationToken cancellationToken)
        {
            var schoolId = _db.CurrentSchoolId;
            return await _db.Rooms.IgnoreQueryFilters()
                .AnyAsync(r => r.SchoolId == schoolId && r.Code == code && (exceptRoomId == null || r.Id != exceptRoomId), cancellationToken);
        }

        public async Task<RoomFeature> AddFeatureAsync(int roomId, int featureLookupId, CancellationToken cancellationToken = default)
        {
            var feature = new RoomFeature { RoomId = roomId, FeatureLookupId = featureLookupId };
            _db.RoomFeatures.Add(feature);

            await _db.SaveChangesAsync(cancellationToken);
            return feature;
        }

        public async Task<Building> UpdateBuildingAsync(int buildingId, string nameAr, string nameEn, CancellationToken cancellationToken = default)
        {
            var building = await _db.Buildings.SingleAsync(b => b.Id == buildingId, cancellationToken);
            building.Name = new LocalizedName(nameAr, nameEn);
            await _db.SaveChangesAsync(cancellationToken);
            return building;
        }

        public async Task DeactivateBuildingAsync(int buildingId, CancellationToken cancellationToken = default)
        {
            var building = await _db.Buildings.SingleAsync(b => b.Id == buildingId, cancellationToken);
            var floors = await _db.Floors.CountAsync(f => f.BuildingId == buildingId, cancellationToken);
            if (floors > 0)
            {
                throw new RoomInUseException(UsageReport.From(new UsageReference("active floor(s) in this building", "طابق فعّال في هذا المبنى", floors)));
            }

            building.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Floor> UpdateFloorAsync(int floorId, int buildingId, string nameAr, string nameEn, int sequenceOrder, CancellationToken cancellationToken = default)
        {
            var floor = await _db.Floors.SingleAsync(f => f.Id == floorId, cancellationToken);
            floor.BuildingId = buildingId;
            floor.Name = new LocalizedName(nameAr, nameEn);
            floor.SequenceOrder = sequenceOrder;
            await _db.SaveChangesAsync(cancellationToken);
            return floor;
        }

        public async Task DeactivateFloorAsync(int floorId, CancellationToken cancellationToken = default)
        {
            var floor = await _db.Floors.SingleAsync(f => f.Id == floorId, cancellationToken);
            var rooms = await _db.Rooms.CountAsync(r => r.FloorId == floorId, cancellationToken);
            if (rooms > 0)
            {
                throw new RoomInUseException(UsageReport.From(new UsageReference("active room(s) on this floor", "قاعة فعّالة في هذا الطابق", rooms)));
            }

            floor.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Room> UpdateRoomAsync(
            int roomId, int floorId, string code, string nameAr, string nameEn, int roomTypeLookupId,
            int standardCapacity, int examCapacity, GenderPolicy wingTag, CancellationToken cancellationToken = default)
        {
            var room = await _db.Rooms.SingleAsync(r => r.Id == roomId, cancellationToken);
            if (!RoomCapacityValidator.IsValidCapacity(standardCapacity, examCapacity))
            {
                throw new InvalidRoomCapacityException();
            }

            if (await IsCodeTakenAsync(code, roomId, cancellationToken))
            {
                throw new DuplicateRoomCodeException(code);
            }

            room.FloorId = floorId;
            room.Code = code;
            room.Name = new LocalizedName(nameAr, nameEn);
            room.RoomTypeLookupId = roomTypeLookupId;
            room.StandardCapacity = standardCapacity;
            room.ExamCapacity = examCapacity;
            room.WingTag = wingTag;
            await _db.SaveChangesAsync(cancellationToken);
            return room;
        }

        public async Task DeactivateRoomAsync(int roomId, CancellationToken cancellationToken = default)
        {
            var room = await _db.Rooms.SingleAsync(r => r.Id == roomId, cancellationToken);
            var sections = await _db.Sections.CountAsync(s => s.DefaultClassroomId == roomId && s.Status == Sms.Domain.Sections.SectionStatus.Active, cancellationToken);
            if (sections > 0)
            {
                throw new RoomInUseException(UsageReport.From(new UsageReference("active section(s) using this room as their classroom — reassign them first", "شعبة فعّالة تتخذ هذه القاعة صفاً لها — أعد إسنادها أولاً", sections)));
            }

            room.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<RoomAvailabilityException> SetUnavailableAsync(
            int roomId, RoomAvailabilityReason reason, DateTime startDate, DateTime endDate, string? notes = null, CancellationToken cancellationToken = default)
        {
            var exception = new RoomAvailabilityException
            {
                RoomId = roomId,
                Reason = reason,
                StartDate = startDate,
                EndDate = endDate,
                Notes = notes,
            };
            _db.RoomAvailabilityExceptions.Add(exception);

            await _db.SaveChangesAsync(cancellationToken);
            return exception;
        }

        public async Task<RoomBooking> RequestBookingAsync(
            int roomId, int academicYearId, string purpose, DateTime startUtc, DateTime endUtc, int requestedByUserId, CancellationToken cancellationToken = default)
        {
            var exceptions = await _db.RoomAvailabilityExceptions
                .Where(e => e.RoomId == roomId)
                .Select(e => new { e.StartDate, e.EndDate })
                .ToListAsync(cancellationToken);

            var isAvailable = RoomAvailabilityChecker.IsAvailable(startUtc, endUtc, exceptions.Select(e => (e.StartDate, e.EndDate)));
            if (!isAvailable)
            {
                throw new RoomUnavailableException(roomId);
            }

            // Displacement of an actual teaching session can't be checked yet
            // (Timetable/M15 doesn't exist) — a free-slot booking auto-approves.
            var booking = new RoomBooking
            {
                AcademicYearId = academicYearId,
                RoomId = roomId,
                Purpose = purpose,
                StartUtc = startUtc,
                EndUtc = endUtc,
                RequestedByUserId = requestedByUserId,
                Status = RoomBookingStatus.Approved,
            };
            _db.RoomBookings.Add(booking);

            await _db.SaveChangesAsync(cancellationToken);
            return booking;
        }
    }
}
