using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Classrooms;
using Sms.Domain.Grades;

namespace Sms.Application.Classrooms
{
    /// <summary>doc/Modules/08 §8 "Room catalog"/"Maintenance console"/"Booking calendar" screens backing (screens deferred, the operations are core).</summary>
    public interface IRoomAdmin
    {
        Task<Building> DefineBuildingAsync(string nameAr, string nameEn, CancellationToken cancellationToken = default);

        Task<Floor> DefineFloorAsync(int buildingId, string nameAr, string nameEn, int sequenceOrder, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.DuplicateRoomCodeException"/> or <see cref="Common.Exceptions.InvalidRoomCapacityException"/>.</summary>
        Task<Room> DefineRoomAsync(
            int floorId, string code, string nameAr, string nameEn, int roomTypeLookupId,
            int standardCapacity, int examCapacity, GenderPolicy wingTag, CancellationToken cancellationToken = default);

        Task<RoomFeature> AddFeatureAsync(int roomId, int featureLookupId, CancellationToken cancellationToken = default);

        /// <summary>BR-ROM-004: records a maintenance/reserved window — the single source of truth for room availability.</summary>
        Task<RoomAvailabilityException> SetUnavailableAsync(
            int roomId, RoomAvailabilityReason reason, DateTime startDate, DateTime endDate, string? notes = null, CancellationToken cancellationToken = default);

        /// <summary>Free-slot bookings auto-approve; a room under maintenance/reserved throws <see cref="Common.Exceptions.RoomUnavailableException"/>. Displacing an actual teaching session can't be checked yet (Timetable/M15 doesn't exist) — see doc comment.</summary>
        Task<RoomBooking> RequestBookingAsync(
            int roomId, int academicYearId, string purpose, DateTime startUtc, DateTime endUtc, int requestedByUserId, CancellationToken cancellationToken = default);
    }
}
