using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Classrooms;
using Sms.Application.Common.Exceptions;
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

            var codeTaken = await _db.Rooms.AnyAsync(r => r.Code == code, cancellationToken);
            if (codeTaken)
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

        public async Task<RoomFeature> AddFeatureAsync(int roomId, int featureLookupId, CancellationToken cancellationToken = default)
        {
            var feature = new RoomFeature { RoomId = roomId, FeatureLookupId = featureLookupId };
            _db.RoomFeatures.Add(feature);

            await _db.SaveChangesAsync(cancellationToken);
            return feature;
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
